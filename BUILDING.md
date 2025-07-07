# Building DAT1 GUI

## Short Version

1. Clone the repo.
2. Open `Overstrike.sln` in Visual Studio.
3. Build and run the project (press **F5**).

> Visual Studio should automatically restore required packages. Make sure you have the .NET toolchain installed.

---

## Longer Version

These instructions assume you're familiar with basic software development. If you're not, it's likely easier to download pre-built binaries from the [Releases](../../releases) page instead.

### 1. Get the Source Code

You can either:

- **Download ZIP**: Click `Code > Download ZIP` on the repository's main page.
- **Clone via Git** (recommended if you plan to contribute):
  ```bash
  git clone https://github.com/Pcniado/DAT1-GUI.git

### 2. Set the configuration to `Debug` or `Release` in the toolbar at the top of Visual Studio.

### 3. If NuGet packages haven’t restored automatically:

- Right-click the solution in **Solution Explorer**
- Select **Restore NuGet Packages**

### 4. Build and run:

- Press **F5** to build and launch the GUI, **or**
- Use the **Build > Build Solution** menu option to compile without running
