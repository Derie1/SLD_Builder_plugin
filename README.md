# SLD Builder — плагин AutoCAD (.NET)

*[English version below](#sld-builder--autocad-plugin-net)*

Плагин строит однолинейную схему распределительного щита в AutoCAD по расчётному файлу Excel
и DWG-шаблону с динамическими блоками. Работает в AutoCAD 2025 и новее.

## Структура

| Путь | Что это |
|---|---|
| `src/SldBuilder.Core` | `net8.0`, без AutoCAD: модели, чтение Excel (ClosedXML), соответствие «тег атрибута ↔ строка DB_EXPORT» (`Mapping/SldLayout.cs`) |
| `src/SldBuilder.Acad` | `net8.0-windows`, плагин: команда `SLDBUILD`, отрисовка (`Drawing/SldDrawer.cs`), окно WPF, строки RU/EN (`Resources/Strings*.resx`) |
| `tests/SldBuilder.Core.Tests` | xUnit, чтение реальных шаблонов xlsx |
| `templates/` | Шаблоны DWG и xlsx, попадают в `Templates/` рядом с DLL |
| `installer/INSTALL.BAT` | Установщик для пользователей |
| `installer/PackageContents/` | Части манифеста автозагрузки: шапка, по блоку на версию AutoCAD (`R25.0.xml` = 2025 …), `ALL.xml`, хвост |

## Принцип работы

1. **Окно.** Команда `SLDBUILD` открывает окно, где выбираются расчётный файл Excel и чертёж: шаблон
   для новой схемы или ранее построенная схема. По умолчанию подставляются шаблоны из папки `Templates`
   рядом с плагином — русские или английские, в зависимости от языка интерфейса.
2. **Чтение Excel.** Данные берутся с листа `AutoCAD` расчётного файла через именованные диапазоны:

   | Диапазон / ячейка | Данные | Блок |
   |---|---|---|
   | `TOTAL` | имя щита и итоги: Pу, Kс, Pр, cos f, Iр, Iкз(3) | `TOTAL` |
   | `BALANCE` | признак «Да/Yes» и нагрузка по фазам L1–L3 | `KNF` |
   | `X_AUTOM`, `Y_AUTOM`, `Y_CABLE`, `AutoCAD!B21:B24`, `AutoCAD!D1` | вводной аппарат, аппарат в вышестоящем щите, кабель, доп. устройство, имя вышестоящего щита | `INCOMER` |
   | `Расчет!U14` | тип вводного аппарата (список `IncomerType`) | `INCOMER` |
   | `DB_EXPORT` | по столбцу на отходящую группу | `AUTOMAT` + `LINE` |

   Используются значения формул, сохранённые Excel, поэтому расчётный файл должен быть сохранён в Excel.
   Файл можно не закрывать. Числа округляются до 2 знаков без лишних нулей, разделитель дробной части —
   по региональным настройкам Windows.
3. **Чертёж.** Для новой схемы создаётся новый чертёж на основе шаблона; сам шаблон не меняется.
   Существующая схема открывается, а если она уже открыта — используется открытый чертёж.
4. **Перестроение.** Перед вставкой удаляются ранее построенные блоки `TOTAL`, `KNF`, `INCOMER`, `AUTOMAT`, `LINE`
   на модели и на всех листах. Остальное оформление чертежа сохраняется, поэтому схему можно обновлять
   после изменений в расчёте.
5. **Вставка блоков.**
   - `TOTAL` — всегда; `KNF` — только если в `BALANCE` стоит «Да» или «Yes».
   - `INCOMER` — состояние видимости `IncomerType` берётся из `Расчет!U14`; если ячейка пустая, используется `SWITCH_3pole`.
   - Для каждого столбца `DB_EXPORT` — пара `AUTOMAT` (видимость `BreakerType`) и `LINE` (видимость `Cunsumer`),
     с шагом 25 единиц по оси X.
   - Атрибуты заполняются по тегам; соответствие тегов и строк Excel задано в `Mapping/SldLayout.cs`.
6. **Сообщения.** Если в чертеже нет нужных блоков или в Excel нет нужного диапазона, построение не начинается
   и выводится сообщение. Некритичные проблемы (ошибка в ячейке Excel, незнакомое состояние видимости, пустой
   тип ввода) выводятся предупреждениями в командную строку, построение продолжается.

Плагин собран под .NET 8 на API AutoCAD 2025 и загружается также в AutoCAD 2026/2027, работающих на .NET 10.
Версии ClosedXML 0.102.2 и DocumentFormat.OpenXml 2.16.0 совпадают с теми, что поставляются с AutoCAD,
чтобы в одном процессе не было двух разных копий этих библиотек.

## Сборка и тесты

```
dotnet test
dotnet build -c Release
```

Release-сборка создаёт:

| Путь | Что это |
|---|---|
| `artifacts/SldBuilder.bundle` | Пакет автозагрузки (все версии с 2025) |
| `artifacts/release/SLD_Builder_<версия>/` | `INSTALL.BAT` + `SldBuilder.bundle.zip` |
| `artifacts/SLD_Builder_<версия>.zip` | То же одним архивом — его и отдавать пользователям |

`dotnet build -c Release -p:DeployBundle=true` дополнительно копирует bundle в `%APPDATA%\Autodesk\ApplicationPlugins`.
Для отладки без bundle: в AutoCAD `NETLOAD` → `src/SldBuilder.Acad/bin/Debug/net8.0-windows/SldBuilder.Acad.dll`, затем `SLDBUILD`.

## Установка у пользователя

1. Распаковать `SLD_Builder_<версия>.zip` в любую папку (не запускать из архива).
2. Закрыть AutoCAD, запустить `INSTALL.BAT`.
3. Выбрать язык установщика: `1` — русский, `2` — English. Все дальнейшие сообщения выводятся на выбранном языке.
4. Установщик покажет найденные версии AutoCAD и спросит год версии (`2026`), `A` — все поддерживаемые,
   `U` — удалить, `Q` — выход. Можно без вопросов: `INSTALL.BAT 2026`, `INSTALL.BAT A`, `INSTALL.BAT U`;
   язык в этом случае задаётся вторым параметром — `INSTALL.BAT 2026 EN` (по умолчанию `RU`).

Что делает установщик:
- ставит плагин в `%APPDATA%\Autodesk\ApplicationPlugins\SldBuilder.bundle` (права администратора не нужны);
- версии до 2025 отклоняет с сообщением «не поддерживается»; год новее известных — просит выбрать `A`;
- если выбранная версия не найдена в реестре (`HKLM\SOFTWARE\Autodesk\AutoCAD\R<серия>`), предупреждает и спрашивает;
- собирает `PackageContents.xml` из частей так, что плагин загружается только в выбранных версиях;
  повторный запуск с другой версией добавляет её (список хранится в `versions.txt` внутри bundle);
- отказывается работать при запущенном `acad.exe` (DLL была бы занята).

**Новая версия AutoCAD:** добавить строку `SERIES_<год>` в `INSTALL.BAT` и файл `installer/PackageContents/R<серия>.xml`.

---

# SLD Builder — AutoCAD plugin (.NET)

The plugin builds a single line diagram of a distribution board in AutoCAD from an Excel calculation file
and a DWG template with dynamic blocks. It runs in AutoCAD 2025 and newer.

## Structure

| Path | What it is |
|---|---|
| `src/SldBuilder.Core` | `net8.0`, no AutoCAD: models, Excel reading (ClosedXML), "attribute tag ↔ DB_EXPORT row" mapping (`Mapping/SldLayout.cs`) |
| `src/SldBuilder.Acad` | `net8.0-windows`, the plugin: `SLDBUILD` command, drawing (`Drawing/SldDrawer.cs`), WPF window, RU/EN strings (`Resources/Strings*.resx`) |
| `tests/SldBuilder.Core.Tests` | xUnit, reading the real xlsx templates |
| `templates/` | DWG and xlsx templates, copied to `Templates/` next to the DLL |
| `installer/INSTALL.BAT` | Installer for users |
| `installer/PackageContents/` | Parts of the autoloader manifest: header, one block per AutoCAD version (`R25.0.xml` = 2025 …), `ALL.xml`, footer |

## How it works

1. **Window.** The `SLDBUILD` command opens a window where the Excel calculation file and the drawing are chosen:
   a template for a new diagram or a previously built diagram. By default the templates from the `Templates` folder
   next to the plugin are suggested — Russian or English, depending on the interface language.
2. **Reading Excel.** Data comes from the `AutoCAD` sheet of the calculation file through named ranges:

   | Range / cell | Data | Block |
   |---|---|---|
   | `TOTAL` | board name and totals: Pi, K, Pd, cos f, Ib, Isc(3) | `TOTAL` |
   | `BALANCE` | "Да/Yes" flag and load per phase L1–L3 | `KNF` |
   | `X_AUTOM`, `Y_AUTOM`, `Y_CABLE`, `AutoCAD!B21:B24`, `AutoCAD!D1` | incoming device, device in the upstream board, cable, additional device, upstream board name | `INCOMER` |
   | `Расчет!U14` | incoming device type (`IncomerType` list) | `INCOMER` |
   | `DB_EXPORT` | one column per outgoing circuit | `AUTOMAT` + `LINE` |

   Formula values cached by Excel are used, so the calculation file must be saved in Excel.
   The file may stay open. Numbers are rounded to 2 decimals without trailing zeros; the decimal separator
   follows the Windows regional settings.
3. **Drawing.** For a new diagram a new drawing is created from the template; the template itself is not changed.
   An existing diagram is opened, or the already open drawing is used.
4. **Rebuilding.** Before inserting, previously built `TOTAL`, `KNF`, `INCOMER`, `AUTOMAT`, `LINE` blocks are erased
   in model space and on all layouts. Everything else in the drawing stays, so the diagram can be updated
   after the calculation changes.
5. **Inserting blocks.**
   - `TOTAL` — always; `KNF` — only if `BALANCE` says "Да" or "Yes".
   - `INCOMER` — the `IncomerType` visibility state comes from `Расчет!U14`; if the cell is empty, `SWITCH_3pole` is used.
   - For every `DB_EXPORT` column — a pair of `AUTOMAT` (visibility `BreakerType`) and `LINE` (visibility `Cunsumer`),
     25 units apart along X.
   - Attributes are filled by tag; the tag ↔ Excel row mapping is in `Mapping/SldLayout.cs`.
6. **Messages.** If the drawing lacks the required blocks or Excel lacks a required range, nothing is built and
   a message is shown. Minor problems (an Excel error in a cell, an unknown visibility state, an empty incomer type)
   are written to the command line as warnings, and building continues.

The plugin is built for .NET 8 against the AutoCAD 2025 API and also loads in AutoCAD 2026/2027, which run on .NET 10.
ClosedXML 0.102.2 and DocumentFormat.OpenXml 2.16.0 match the versions shipped with AutoCAD,
so that the process never holds two different copies of these libraries.

## Build and tests

```
dotnet test
dotnet build -c Release
```

The Release build produces:

| Path | What it is |
|---|---|
| `artifacts/SldBuilder.bundle` | Autoloader package (all versions from 2025) |
| `artifacts/release/SLD_Builder_<version>/` | `INSTALL.BAT` + `SldBuilder.bundle.zip` |
| `artifacts/SLD_Builder_<version>.zip` | The same as one archive — this is what users get |

`dotnet build -c Release -p:DeployBundle=true` also copies the bundle to `%APPDATA%\Autodesk\ApplicationPlugins`.
For debugging without the bundle: in AutoCAD `NETLOAD` → `src/SldBuilder.Acad/bin/Debug/net8.0-windows/SldBuilder.Acad.dll`, then `SLDBUILD`.

## Installation for users

1. Unpack `SLD_Builder_<version>.zip` to any folder (do not run it from inside the archive).
2. Close AutoCAD and run `INSTALL.BAT`.
3. Choose the installer language: `1` — Russian, `2` — English. All further messages are shown in that language.
4. The installer shows the AutoCAD versions it found and asks for the version year (`2026`), `A` — all supported,
   `U` — uninstall, `Q` — quit. Without questions: `INSTALL.BAT 2026`, `INSTALL.BAT A`, `INSTALL.BAT U`;
   the language is then given as the second parameter — `INSTALL.BAT 2026 EN` (default `RU`).

What the installer does:
- installs the plugin to `%APPDATA%\Autodesk\ApplicationPlugins\SldBuilder.bundle` (no administrator rights needed);
- rejects versions before 2025 with a "not supported" message; for a year newer than the known ones it asks to choose `A`;
- if the chosen version is not found in the registry (`HKLM\SOFTWARE\Autodesk\AutoCAD\R<series>`), it warns and asks;
- assembles `PackageContents.xml` from the parts so that the plugin loads only in the chosen versions;
  running it again with another version adds that version (the list is kept in `versions.txt` inside the bundle);
- refuses to run while `acad.exe` is running (the DLL would be locked).

**New AutoCAD version:** add a `SERIES_<year>` line to `INSTALL.BAT` and the file `installer/PackageContents/R<series>.xml`.
