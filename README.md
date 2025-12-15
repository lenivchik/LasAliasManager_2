# LAS Curve Alias Manager - Avalonia GUI

A cross-platform desktop application for managing curve name aliases in LAS (Log ASCII Standard) files.

## Features

- **Load and analyze LAS files** from entire folders (with optional recursion into subfolders)
- **Interactive table view** showing:
  - Curve Field Name (from LAS file)
  - Primary Name (dropdown with all available base names)
  - File Name and Size
  - Well Information: Top (STRT), Bottom (STOP), Step
- **Visual status indicators** for unknown, modified, and ignored curves
- **Filter and search** capabilities
- **Batch operations** for unknown curves
- **Cross-platform** - runs on Windows, macOS, and Linux
- **CSV database format** - single file for all alias definitions

## Requirements

- .NET 8.0 SDK or later
- For development: Visual Studio 2022, JetBrains Rider, or VS Code with C# extensions

## Building

### From Command Line

```bash
cd LasAliasManager
dotnet restore
dotnet build
```

### Running the GUI

```bash
dotnet run --project LasAliasManager.GUI
```

### Running the TXT to CSV Converter

```bash
dotnet run --project LasAliasManager.Converter
```

Or with command-line arguments:
```bash
dotnet run --project LasAliasManager.Converter -- "ignored.txt" "primary_names.txt" "aliases.txt" "output.csv"
```

## TXT to CSV Converter

The converter tool converts your existing TXT files to the new CSV format.

### Input Files

1. **File 1 - Ignored Names** (e.g., `ListNameAlias_NO.txt`): List of curve names to ignore
2. **File 2 - Primary Names**: List of base/primary names only
3. **File 3 - Alias File** (e.g., `ListNameAlias.txt`): Primary names with their field name aliases. If PrimaryName is "No", the field name is marked as ignored.

### Output CSV Format

```csv
FieldName,PrimaryName,Status,Description
GR,GR,base,
GR_CORR,GR,alias,
GAMMA,GR,alias,
NPHI,NPHI,base,
TEMP_TOOL,,ignore,
```

**Status values:**
- `base` - Primary/base name (FieldName equals PrimaryName)
- `alias` - Field name that maps to a primary name
- `ignore` - Field name to be ignored during analysis

### Publishing a Self-Contained Executable

**Windows:**
```bash
dotnet publish LasAliasManager.GUI -c Release -r win-x64 --self-contained
```

**macOS:**
```bash
dotnet publish LasAliasManager.GUI -c Release -r osx-x64 --self-contained
```

**Linux:**
```bash
dotnet publish LasAliasManager.GUI -c Release -r linux-x64 --self-contained
```

## Usage

### 1. Load Alias Database

Click **"Load Database"** and select:
1. First: The `ListNameAlias.txt` file (contains base names and their aliases)
2. Second: The `ListNameAlias_NO.txt` file (contains names to ignore)

### 2. Select LAS Files Folder

Click **"Select Folder"** and choose a directory containing LAS files. The application will:
- Scan all `.las` files (optionally including subfolders)
- Extract curve names and well information (STRT, STOP, STEP)
- Classify each curve as: mapped, unknown, or ignored

### 3. Map Unknown Curves

For each unknown curve (shown with ❓), select a Primary Name from the dropdown:
- Choose an existing base name to add as an alias
- Select `[IGNORE]` to add to the ignored list
- Select `[NEW BASE]` to create a new base name

### 4. Save Changes

Click **"Save"** to write all changes back to the database files.

## Table Columns

| Column | Description |
|--------|-------------|
| Curve Field Name | The mnemonic from the LAS file's ~C section |
| Primary Name | Dropdown to select/assign the standardized base name |
| File Name | Name of the source LAS file |
| Size | File size in human-readable format |
| Top (STRT) | Start depth from ~W section |
| Bottom (STOP) | Stop depth from ~W section |
| Step | Step value from ~W section |
| Unit | Depth unit (M, FT, etc.) |
| Status | Visual indicator (✓ mapped, ❓ unknown, ✏️ modified, 🚫 ignored) |

## Filtering

- **Show Only Unknown/Modified**: Toggle to filter the table
- **Search box**: Filter by curve name, file name, or primary name

## Project Structure

```
LasAliasManager/
├── LasAliasManager.sln           # Solution file
├── LasAliasManager.Core/         # Shared library
│   ├── Models/
│   │   ├── AliasDatabase.cs
│   │   └── CurveAlias.cs
│   └── Services/
│       ├── AliasFileParser.cs
│       ├── AliasFormatConverter.cs  # TXT to CSV converter
│       ├── AliasManager.cs
│       └── LasFileParser.cs
├── LasAliasManager.GUI/          # Avalonia GUI application
│   ├── App.axaml
│   ├── Program.cs
│   ├── ViewModels/
│   │   ├── CurveRowViewModel.cs
│   │   └── MainWindowViewModel.cs
│   └── Views/
│       └── MainWindow.axaml
└── LasAliasManager.Converter/    # TXT to CSV converter tool
    └── Program.cs
```

## Dependencies

- **Avalonia** 11.1.0 - Cross-platform UI framework
- **CommunityToolkit.Mvvm** - MVVM helpers
- **MessageBox.Avalonia** - Dialog boxes
- **Ude.NetStandard** - Character encoding detection

## License

MIT License
