# Power Node List Generator (C++ backend + C# WinForms UI)
* **Nutshell** : Download `run.zip` from the [release page], and in the folder, run `PowerNodeGenUI.exe` to generate a Power Node List CSV from  export files with keyword filtering and optional diffing against an imported old CSV. 
* Better to run at display 100/125% :(
## Brief Introduction
This project generates a **Power Node List** from PCB/EDA export files (e.g., `Nets.asc`, `Nails.asc`) and a **user-configurable keyword list**.  
It consists of:

- **C++ backend** (`PowerNodeGen.exe`): parses input files, applies keyword filtering, and outputs a CSV.
- **C# WinForms UI**: helps users select inputs, configure keywords, run the backend, preview results, and compare against an imported former CSV.

## Features

### 1) Generate Power Node List (core)
- Select input files:
  - `Nets.asc`
  - `Nails.asc`
- Select output file directory
- Configure power keywords:
  - **Include** list (required)
  - **Exclude** list (optional)
  - Type in in the box and press **Enter** to add in list
    - Also supporting separating with `,`, ` `, `;`
  - Click on the keyword and press **Remove** to remove from list
  - **Clear** to clear the list
- Click **Submit** to generate `PowerNodeList.csv`
- Preview results in a grid (DataGridView)
  - **View** for detailed dialog to view/search pins for a selected net.
- **Open CSV** button launches the output CSV

### 2) Optional filter: “Only show nets with nails”
- UI checkbox: **Only show nets with nails**
- Meaning: output only nets where `nail_id != 0`, while still respecting include/exclude keyword matching.

### 3) Compare with an old Power Node List CSV (diff)
- Click **Import Old CSV**
- The tool compares **Old CSV vs New (current grid)** and pops a result table.
- Diff status per row:
  - `Added` / `Removed` / `Modified` / `Unchanged`
- Built-in filter (dropdown) to show only specific statuses.

---

## Implementation Notes

### A) Keyword configuration file
When you click **Submit**, the UI generates a temporary keyword config file:

- `+<keyword>` lines are **Include** rules
- `-<keyword>` lines are **Exclude** rules

The file is written in **UTF-8 without BOM** to avoid first-line parsing issues in C++.

### B) Backend command line
The UI launches the backend in a separate process:

```
PowerNodeGen.exe --nets "<path to Nets.asc>" --nails "<path to Nails.asc>" --cfg "<keyword cfg>" --out "<output csv>" [--only-nails]
```

### C) CSV preview and actions
After the backend finishes:
- the UI loads CSV into a `DataTable`
- binds the table to `DataGridView`
- hides `related_pins_list` by default (too long)
- adds a `Pins` button column to open a pins dialog

### D) Compare
On **Import Old CSV**:
- Old CSV → `DataTable oldDt`
- New CSV → current grid `DataTable newDt`
- The tool compares using **`net_id`** if available on both sides; otherwise falls back to `net_name`.
- Differences are categorized into:
  - `Added`   = in new, not in old
  - `Removed` = in old, not in new
  - `Modified`= same key exists, but important fields differ
  - `Unchanged`

Recommended field comparison:
- `nail_id`
- `related_pins_cnt`
- (optional) treat `related_pins_list` as “changed or not” because it can be large

---
## User Guide 

### Generate a new Power Node List

1. **Download** `run.zip` from the release page.
2. **Unzip** the folder and run `PowerNodeGenUI.exe`.
3. **Launch** the UI application.
4. Click **Browse** and select the following files:
* `Nets.asc`
* `Nails.asc`


5. **Set the Output CSV path** (the default location is your Desktop as `PowerNodeList.csv`).
6. **Add Include keywords**: Type a keyword and press **Enter**.
7. **Optionally add Exclude keywords** using the same method.
8. **Optional**: Check the box for **Only show nets with nails** if needed.
9. Click **Submit**.
10. **Review the results**:
* The grid will display the final data.
* Click **Open CSV** to launch the output file.
* Click the **Pins** column to view the specific pins list.
---

### Compare with an older CSV
1. Generate/load a new list first (so the grid contains the “New” list).
2. Click **Import Old CSV**.
3. Select an older `PowerNodeList.csv`.
4. A compare window appears:
   - Filter by status (Added/Removed/Modified/Unchanged)
   - Use this to review what changed between runs/versions.

---

## Input / Output Formats

### Input
Current backend usage focuses on:
- `Nets.asc`
- `Nails.asc`

### Output CSV (typical columns)
- `net_id`
- `net_name`
- `nail_id`
- `related_pins_cnt`
- `related_pins_list`

---

## Troubleshooting

### “Cannot find PowerNodeGen.exe”
Make sure `PowerNodeGen.exe` is in the same folder as the UI executable.

### “Include keywords is empty”
At least one **Include** keyword is required to run.

### Output CSV not generated
- Confirm paths are correct
- Check backend stderr in a debug build
- Make sure output folder is writable
- Might want to close the .csv first

### CSV preview looks wrong
If your CSV contains quotes/commas inside fields, ensure backend outputs valid CSV quoting (UI parser supports quoted CSV).

### Abnormal display
Try to change screen to 100% scaling (DPI) and restart the exe.

---
