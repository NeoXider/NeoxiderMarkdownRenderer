# Neoxider Markdown Renderer

Markdig-based Markdown renderer for Unity **UI Toolkit**. Turns Markdown text into a live `VisualElement` tree —
no HTML or WebView — so it can sit in any runtime UI (chat bubbles, quiz text, docs panels) or power Editor tools.

## Install

Unity 6000.0 or newer. Package Manager → *Add package from git URL*:

```text
https://github.com/NeoXider/NeoxiderMarkdownRenderer.git#v1.2.0
```

or in `Packages/manifest.json`:

```json
"com.neoxider.markdownrenderer": "https://github.com/NeoXider/NeoxiderMarkdownRenderer.git#v1.2.0"
```

## What is inside

- `UIMarkdownRenderer` — headings, bold/italic, inline code, blockquotes, nested lists, links, thematic breaks and
  fenced code blocks with syntax highlighting (Python, C#).
- `MarkdownDisplay` (`[UxmlElement]`) — ready-to-use runtime control: drop it into UXML or create it in code, set
  `Markdown`/`SourceAsset`. Its stylesheet is a serialized reference, so it renders the same in the Editor, Play Mode
  and builds (WebGL, standalone).
- `VideoPlayerElement` (`[UxmlElement]`) — inline video embeds.
- `YamlFrontMatterHandler` — optional YAML front matter ahead of the Markdown body.
- Syntax highlighting: `PythonSyntaxHighlighter`, `CSharpSyntaxHighlighter`; colours come from JSON palettes
  (`SyntaxPalette.Load(name)` reads `Resources/SyntaxHighlighting/<name>.json`), not from code.
- Editor tooling: rendered inspector for `.md` assets, `Window/Markdown Doc Viewer` (anchors, `Assets/…`/`Packages/…`
  links, `search:`/`package:` lookups, `cmd:` commands), `Window/Neoxider/Markdown Preview` (raw text vs live
  render), media pop-out window.
- Default styles `Styles/MarkdownRenderer.uss` and `Styles/Quiz.uss` — optional; render with
  `includeDefaultStylesheet: false` and bring your own theme.
- `Examples/*.md` — sample documents.

## Usage

```csharp
var renderer = new UIMarkdownRenderer((link, r) => Application.OpenURL(link));
renderer.SetMarkdown("# Hello\n\nSome **markdown**.");
myVisualElement.Add(renderer.RootElement);
```

```csharp
var display = new MarkdownDisplay { DefaultStyleSheet = myStylesheet };
display.LinkClicked += link => Application.OpenURL(link);
display.SetMarkdown("# Hello\n\nSome **markdown**.");
myVisualElement.Add(display);
```

Per-instance options: `InlineCodeColor`, `InlineCodeInheritsTextColor`, `LinkInheritsTextColor`.

## Assemblies

`Rtl.MarkdownRenderer.Runtime`, `Rtl.MarkdownRenderer.Editor` (Editor only), `Markdig` (vendored parser),
`Rtl.MarkdownRenderer.Tests.EditMode`.

## License

- Modifications and additions by Neoxider — **MIT**.
- Based on [Markdown UIElement](https://github.com/UnityGuillaume/MarkdownRenderer) by Guillaume Saby,
  © 2023 Unity Technologies — the original parts remain under the
  [Unity Companion License](https://unity3d.com/legal/licenses/unity_companion_license) (Unity-dependent projects).
- Markdig — BSD-2-Clause, © Alexandre Mutel; see `Third Party Notices.md`.

Full text: [LICENSE.md](LICENSE.md).
