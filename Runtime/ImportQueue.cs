﻿using System;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Text.RegularExpressions;

using NorskaLib.GoogleSheetsDatabase.Utils;
namespace NorskaLib.GoogleSheetsDatabase
{
    public class ImportQueue
    {
        public const string URLFormat = @"https://docs.google.com/spreadsheets/d/{0}/gviz/tq?tqx=out:csv&sheet={1}";

        private readonly DataContainerBase container;
        private readonly FieldInfo[] listsInfos;
        private readonly string documentID;

        public Action<DataContainerBase> onComplete;

        public bool abort;

        private string output;
        public string Output
        {
            get => output;

            private set
            {
                output = value;
                onOutputChanged.Invoke();
            }
        }
        public Action onOutputChanged;

        public float progress;
        public float Progress
        {
            get => progress;

            private set
            {
                progress = Mathf.Clamp01(value);
                onProgressChanged.Invoke();
            }
        }
        public Action onProgressChanged;

        private float ProgressElementDelta
            => 1f / listsInfos.Length;

        public ImportQueue(DataContainerBase container, FieldInfo[] listsInfos)
        {
            this.container = container;
            this.listsInfos = listsInfos;
            this.documentID = container.documentID;
        }

        public async void Run()
        {
            abort = false;
            var webClient = new WebClient();

            for (int i = 0; i < listsInfos.Length && !abort; i++)
                await PopulateList(container, listsInfos[i], webClient);

            webClient.Dispose();

            onComplete.Invoke(container);
        }

        private async Task PopulateList(DataContainerBase container, FieldInfo fieldInfo, WebClient webClient)
        {
            Type fieldType = fieldInfo.FieldType;
            bool isList = typeof(IList).IsAssignableFrom(fieldType);
            Type contentType = isList
                ? fieldType.GetGenericArguments().SingleOrDefault()
                : fieldType;

            if (contentType is null)
            {
                Debug.LogError($"Could not identify type of defs stored in {fieldInfo.Name}");
                return;
            }

            #region Downloading page

            var googleSheetRef = (PageNameAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(PageNameAttribute));
            var pagename = googleSheetRef.name;

            Output = $"Downloading page '{pagename}'...";
            var url = string.Format(URLFormat, documentID, pagename);

            Task<string> request;
            try
            {
                request = webClient.DownloadStringTaskAsync(url);
            }
            catch (WebException)
            {
                Debug.LogError($"Bad URL '{url}'");
                abort = true;
                throw;
            }

            while (!request.IsCompleted)
                await Task.Delay(100);

            var rawTable = Regex.Split(request.Result, "\r\n|\r|\n");
            request.Dispose();

            Progress += 1 / 3f * ProgressElementDelta;

            #endregion

            #region Analyzing and splitting raw text

            Output = $"Analysing headers...";

            var headersRaw = Utilities.Split(rawTable[0]);

            var idHeaderIdx = -1;
            var headers = new List<string>();
            var emptyHeadersIdxs = new List<int>();

            for (int i = 0; i < headersRaw.Length; i++)
            {
                if (string.IsNullOrEmpty(headersRaw[i]))
                {
                    emptyHeadersIdxs.Add(i);
                    continue;
                }

                if (idHeaderIdx == -1 && headersRaw[i].ToLower() == "id")
                    idHeaderIdx = i;

                headers.Add(headersRaw[i]);
            }

            var rows = new List<string[]>();
            for (int i = 1; i < rawTable.Length; i++)
            {
                var substrings = Utilities.Split(rawTable[i]);
                if (idHeaderIdx != -1 && string.IsNullOrEmpty(substrings[idHeaderIdx]))
                    continue;

                rows.Add(substrings.Where((val, index) => !emptyHeadersIdxs.Contains(index)).ToArray());
            }

            Progress += 1 / 3f * ProgressElementDelta;

            #endregion

            #region Parsing and populating field

            Output = $"Populating {(isList ? "list" : "single object")} of defs '{fieldInfo.Name}'<{contentType.Name}>...";

            var headersToFields = new Dictionary<string, FieldInfo>();
            foreach (var h in headers)
            {
                if (h.StartsWith("_")) continue;

                var field = contentType.GetField(h, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                {
                    Debug.LogWarning($"Header '{h}' match no field in {contentType.Name} type");
                    continue;
                }
                headersToFields.Add(h, field);
            }

            if (isList)
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(contentType));
                foreach (var row in rows)
                {
                    var item = Activator.CreateInstance(contentType);

                    for (int i = 0; i < headers.Count; i++)
                    {
                        if (headersToFields.TryGetValue(headers[i], out var field))
                        {
                            var value = Utilities.Parse(row[i], field.FieldType);
                            field.SetValue(item, value);
                        }
                    }

                    list.Add(item);
                }

                fieldInfo.SetValue(container, list);
            }
            else
            {
                if (rows.Count == 0)
                {
                    Debug.LogWarning($"No data found for single object field '{fieldInfo.Name}'");
                    return;
                }

                var item = Activator.CreateInstance(contentType);
                var row = rows[0];

                for (int i = 0; i < headers.Count; i++)
                {
                    if (headersToFields.TryGetValue(headers[i], out var field))
                    {
                        var value = Utilities.Parse(row[i], field.FieldType);
                        field.SetValue(item, value);
                    }
                }

                fieldInfo.SetValue(container, item);
            }

            Progress += 1 / 3f * ProgressElementDelta;

            #endregion
        }
    }
}
