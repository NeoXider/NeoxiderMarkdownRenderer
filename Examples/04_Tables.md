# Tables

This file checks Markdown pipe tables: structure, column alignment, inline formatting inside cells, wrapping and edge cases.

## Basic table

| Key | Value |
| --- | --- |
| Renderer | UIMarkdownRenderer |
| Target | Editor + Runtime + WebGL |
| Parser | Markdig pipe tables |

## Column alignment

The separator row sets the alignment: `:---` left, `:---:` center, `---:` right.

| Item | Status | Price |
| :--- | :---: | ---: |
| Sword | ready | 120 |
| Shield | draft | 85 |
| Potion | ready | 7 |

## Inline formatting inside cells

| Syntax | Result |
| --- | --- |
| Bold and italic | **bold**, *italic* |
| Inline code | call `Debug.Log("x")` |
| Link | [Unity UI Toolkit manual](https://docs.unity3d.com/Manual/UIElements.html) |
| Escaped pipe | a \| b |

## Long cells wrap

Columns share the width by the length of their longest content, so a long cell wraps inside its own column and the table never gets wider than its parent.

| Module | Topic | What it does | Docs |
| :--- | :---: | --- | ---: |
| M1 | Basics | Short cell | [doc](https://example.com/m1) |
| M2 | Loops | This cell is long on purpose: the text must wrap inside its own column instead of pushing the neighbouring columns outside the parent, no matter how many words it has. | [doc](https://example.com/m2) |
| M3 | Code | A call to `Debug.Log("x")` in a cell | none |
| M4 | Empty |  | [doc](https://example.com/m4) |
| M5 | Short | ok | none |

## Short rows and empty cells

A row with fewer cells than the header is padded with empty cells, so the columns stay aligned.

| A | B | C |
| --- | --- | --- |
| 1 |
| 4 |  | 6 |

## Table inside a list

Inside a list item the table falls back to readable text rows (a list is rendered as a single text label):

- Settings

  | Key | Value |
  | --- | --- |
  | mode | `fast` |
  | retries | 3 |

- Next item

## Not a table

Pipes without a separator row stay plain text: left | middle | right.
