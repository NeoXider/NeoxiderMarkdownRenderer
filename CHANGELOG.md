# Neoxider Markdown Renderer Changelog

## 1.2.0 — 2026-10-28

- Отдельный публичный репозиторий https://github.com/NeoXider/NeoxiderMarkdownRenderer. Лицензия: изменения и
  дополнения Neoxider — MIT; основа — Markdown UIElement (© 2023 Unity Technologies, Unity Companion License);
  Markdig — BSD-2-Clause (`LICENSE.md`, `Third Party Notices.md`).
- Пути редакторных ассетов (`Styles/MarkdownRenderer.uss`, шаблон и стиль `VideoPlayerElement`) — от корня
  пакета (`PackageInfo`, для встроенной копии — по скрипту), а не `Assets/ThirdParty/...`: пакет работает и
  установленным из git в `Packages`.
- Удалён неиспользуемый `Editor/AlgoNeoPackageAssetLocator.cs` (остаток исходного ассета).

- `UIMarkdownRenderer.InlineCodeInheritsTextColor` и `LinkInheritsTextColor`: встроенный код и ссылка не пишут
  тег `<color>` и берут цвет текста от USS (код остаётся жирным, ссылка подчёркнутой). Нужны поверхностям,
  где весь текст одного цвета (плашки вопроса и ответа комнат). Поведение по умолчанию не изменено.

## 1.1.1

- Мигрирован `VideoPlayerElement` на UI Toolkit UXML API Unity 6.6: удалена legacy-фабрика
  `UxmlFactory<VideoPlayerElement>`, сам элемент помечен `[UxmlElement]` и объявлен `partial`.
  Публичных UXML-атрибутов у элемента не было, поэтому контракт UXML не расширялся. Полный аудит
  Runtime, Editor, Examples, Tests и `.uxml` не нашёл других `UxmlFactory`, `UxmlTraits`,
  `Uxml*AttributeDescription`, `IUxmlAttributes` или legacy `Init(...)`.
- Имя тега в `Runtime/VideoElement/VideoPlayerElement.uxml` (`<VideoPlayerElement>`, глобальный
  namespace) оставлено прежним — `[UxmlElement]` регистрирует элемент под тем же полным именем, что
  и снятая фабрика, поэтому `UIMarkdownRenderer.AddVideoPlayer` продолжает находить его через
  `Q<VideoPlayerElement>()`. Внимание: пока сборка проекта была сломана, Unity импортировала этот
  `.uxml` без доступного типа и закешировала элемент как заглушку-`Label` («Element
  'VideoPlayerElement' is missing a UxmlElementAttribute»). После того как проект соберётся целиком и
  пройдёт domain reload, `.uxml` нужно переимпортировать (Reimport), иначе видеоплеер в Markdown
  соберётся без панели управления.
- Минимальная версия Unity в `package.json` осознанно оставлена на `6000.0`: `[UxmlElement]` доступен
  начиная с Unity 6000.0, в 6.6 сломался ровно старый путь, а не новый. Мигрированный элемент собирается
  на всём диапазоне 6000.0+, поэтому сужать поддержку незачем.

## 1.1.0

- Verified the package compiles clean (zero console errors/warnings) on the project's current Unity
  `6000.5.0f1`. No source changes were needed: the `EntityId`-based double-click asset lookup in
  `MarkdownViewer` already branches on `UNITY_6000_3_OR_NEWER`, and the `UNITY_2022_x` reflection
  fallbacks in `UIMarkdownRenderer` are inert on this project (both take the modern branch). Documented
  these version guards, and flagged `Editor/AlgoNeoPackageAssetLocator.cs` as unreferenced vendor
  leftover (wrong package name/paths for this layout), in the README.
- Added `MarkdownDisplay`, a runtime `[UxmlElement]` control (`Runtime/MarkdownRendering/MarkdownDisplay.cs`)
  that wraps `UIMarkdownRenderer` behind `Markdown`/`SourceAsset`/`DefaultStyleSheet`/
  `IncludeDefaultStylesheet` UXML attributes and a `LinkClicked` event, reusing a single renderer
  instance across re-renders. Unlike the Editor tools, its stylesheet is a serialized reference rather
  than an `AssetDatabase` path, so it works identically in builds (WebGL/standalone/etc), not just the
  Editor. Verified live: renders, re-renders on a second `SetMarkdown` call without rebuilding the
  renderer, and reports link clicks — zero console errors/warnings.

## 1.0.0

- Formalized as a standalone Neoxider developer package: added `package.json`
  (`com.neoxider.markdownrenderer`), `README.md` and this changelog, matching the layout already
  used by the sibling `Neoxider LevelGeneration`/`CoreAiPro` packages under
  `Assets/ThirdParty/Neoxider`.
- Added `MarkdownPreviewWindow` (`Window/Neoxider/Markdown Preview`): a split-pane editor tool (raw
  Markdown on the left, live render on the right, with a size slider) for iterating on the renderer
  itself using only the package's own default stylesheet — no host project theme dependency. This is
  the package-owned counterpart to project-specific preview tools that layer their own theme on top
  of `UIMarkdownRenderer` (e.g. RedoSchool's `ChatUI/Editor/MarkdownStylePreviewWindow`, which renders
  through the same pipeline but with the host project's chat-bubble theme instead of the default
  stylesheet).
- No behavioral changes to the renderer, object renderers, syntax highlighters, or existing
  `MarkdownCustomEditor`/`MarkdownViewer`/`MediaPopoutWindow` tools — this release only adds the
  package metadata and the new preview window on top of the existing, already-shipping code.
