# UnityGoogleSheetsDatabase
Unity Editor tool, which helps design game-logic database (such as characters, weapons, items stats etc.) using Google Spreadsheets.

# Disclaimer
This package has been included as a module into [NorskaLib](https://github.com/NorskaGames/NorskaLibUPM/tree/master/Spreadsheets) repository; this repository will not receive updates!

# How to
## Install the Package
### via Package Manager
To install this package using Unity Package Manager simply insert this address:
```
https://github.com/NorskaGames/UnityGoogleSheetsDatabase.git
```
![package-manager-setup](https://drive.google.com/uc?id=16GE1j46xtedQu88d6cfM2G4itvqxpU2f)

## Setup the Spreadsheet

Create your Spreadsheet and make sure you enable access via link:
![spreadsheet-design](https://drive.google.com/uc?id=12Zo-_fQFYK8n9ljWMkfWtwbYhUUCP7ks)

_**Hint:** Design your database as any relational database (at least stick to 1st normal form: avoid lists inside a cell)._
![db-design-practices](https://drive.google.com/uc?id=1cGzRClYvEsvtzYkAlZp_nDVymvRPsjS1)

## Setup the Container

Define your custom DataContainer and any amount of Data classes as shown below:
```
using NorskaLib.GoogleSheetsDatabase;

[CreateAssetMenu(fileName = "DataContainer", menuName = "Custom/DataContainer")]
public class DataContainer : DataContainerBase
{
    [PageName("SomeSpreadsheetPage")]
    public List<ExampleData> ExampleData;
}

[System.Serializable]
public class ExampleData
{
    public string Id;

    public float SomeFloat;
    public int SomeInt;
    public string SomeString;
}
```
_**Important!** Make sure you spell variables names exactly as columns headers in the spreadsheet._

Now you can create DataContainer asset which looks like this:
![container-inspector](https://drive.google.com/uc?id=16Rg4NIyj5c8-Qjq5phW0konDMRMKNN21)
