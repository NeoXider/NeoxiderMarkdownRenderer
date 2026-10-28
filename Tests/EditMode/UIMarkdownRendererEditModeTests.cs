using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UIMarkdownRenderer.ObjectRenderers;
using UIMarkdownRenderer.ObjectRenderers.SyntaxHighlighting;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rtl.MarkdownRenderer.Tests.EditMode
{
    public class UIMarkdownRendererEditModeTests
    {
        [Test]
        public void SetMarkdown_RendersPythonFencedCodeBlockWithHighlighting()
        {
            global::UIMarkdownRenderer.UIMarkdownRenderer renderer = CreateRenderer();

            renderer.SetMarkdown("```python\nprint(\"x\")\nprice = 1990\n```");

            VisualElement codeBlock = SingleElementWithClass(renderer.ContentRoot, "codeblock");
            Assert.That(codeBlock.ClassListContains("language-python"), Is.True);

            string text = JoinedLabelText(codeBlock);
            // Палитра берётся из источника, чтобы смена темы не ломала проверку токенизации.
            SyntaxPalette palette = SyntaxPalette.Load("python");
            StringAssert.Contains($"<color={palette.Function}>print</color>", text);
            StringAssert.Contains($"<color={palette.String}>\"x\"</color>", text);
            StringAssert.Contains($"<color={palette.Number}>1990</color>", text);
        }

        [Test]
        public void Python_СкобкиВложенногоКодаСохраняютсяИНеПерекрашиваютСтрокиИКомментарии()
        {
            SyntaxPalette palette = new() { Bracket = "#123456", String = "#234567", Comment = "#345678" };
            PythonSyntaxHighlighter highlighter = new(palette);
            string source = "print([{'x': \"([{}])\"}]) # ([{}])";

            string highlighted = highlighter.Highlight(new[] { source })[0];

            Assert.That(Regex.Replace(highlighted, "</?color(?:=[^>]+)?>", ""), Is.EqualTo(source),
                "Подсветка не должна менять исходный код или порядок вложенных скобок.");
            foreach (char bracket in "([{}])")
            {
                StringAssert.Contains($"<color={palette.Bracket}>{bracket}</color>", highlighted,
                    "Синтаксические скобки должны получать собственный токен палитры.");
            }

            StringAssert.Contains($"<color={palette.String}>\"([{{}}])\"</color>", highlighted,
                "Скобки внутри строки остаются строкой, включая парные кавычки.");
            StringAssert.Contains($"<color={palette.Comment}># ([{{}}])</color>", highlighted,
                "Скобки в комментарии не становятся синтаксическими токенами.");
            Assert.That(Regex.Matches(highlighted, $"<color={palette.Bracket}>").Count, Is.EqualTo(6),
                "Ровно шесть синтаксических скобок находятся вне строки и комментария.");
        }

        [Test]
        public void SetMarkdown_RendersCSharpFencedCodeBlockWithHighlighting()
        {
            global::UIMarkdownRenderer.UIMarkdownRenderer renderer = CreateRenderer();

            renderer.SetMarkdown("```csharp\npublic void Run()\n{\n    Debug.Log(\"ok\");\n}\n```");

            VisualElement codeBlock = SingleElementWithClass(renderer.ContentRoot, "codeblock");
            Assert.That(codeBlock.ClassListContains("language-csharp"), Is.True);

            string text = JoinedLabelText(codeBlock);
            // public/void=keyword, Run=function (call), "ok"=string.
            StringAssert.Contains($"<color={VsCodePalette.Keyword}>public</color>", text);
            StringAssert.Contains($"<color={VsCodePalette.Keyword}>void</color>", text);
            StringAssert.Contains($"<color={VsCodePalette.Function}>Run</color>", text);
            StringAssert.Contains($"<color={VsCodePalette.String}>\"ok\"</color>", text);
        }

        [Test]
        public void SetMarkdown_RendersInlineCodeWithEscapedRichTextCharacters()
        {
            global::UIMarkdownRenderer.UIMarkdownRenderer renderer = CreateRenderer();

            renderer.SetMarkdown("Use `a < b & c` in a sentence.");

            string text = JoinedLabelText(renderer.ContentRoot);
            StringAssert.Contains($"<color={VsCodePalette.InlineCode}><b>a <\u200B b &\u200B c</b></color>", text);
        }

        [Test]
        public void ЦветInlineКода_ИзолированОтДругогоРендераИБлоковPython()
        {
            global::UIMarkdownRenderer.UIMarkdownRenderer renderer = CreateRenderer();
            global::UIMarkdownRenderer.UIMarkdownRenderer other = CreateRenderer();
            Color defaultColor = other.InlineCodeColor;
            renderer.InlineCodeColor = new Color32(12, 34, 56, 255);
            SyntaxPalette python = SyntaxPalette.Load("python");

            renderer.SetMarkdown("`value`\n\n```python\nprint('value')\n```");

            string text = JoinedLabelText(renderer.ContentRoot);
            StringAssert.Contains($"<color=#{ColorUtility.ToHtmlStringRGB(renderer.InlineCodeColor)}><b>value</b></color>", text,
                "Встроенный код должен использовать цвет своего потребителя.");
            StringAssert.Contains($"<color={python.Function}>print</color>", text,
                "Палитра блока Python не меняется вместе с цветом встроенного кода.");
            Assert.That(other.InlineCodeColor, Is.EqualTo(defaultColor),
                "Настройка одного сообщения не должна перекрашивать другие панели.");
        }

        [Test]
        public void SetMarkdown_ВыделениеНеМеняетСемантикуКодаСсылкиИЗаголовка()
        {
            global::UIMarkdownRenderer.UIMarkdownRenderer renderer = CreateRenderer();

            renderer.SetMarkdown("# Заголовок\n\n**важно** и `value`, [ссылка](https://example.com).");

            string text = JoinedLabelText(renderer.ContentRoot);
            StringAssert.Contains("<b>важно</b>", text, "Выделение передаёт насыщенность шрифту без перекраски текста.");
            StringAssert.Contains($"<color={VsCodePalette.InlineCode}><b>value</b></color>", text,
                "Код использует собственный токен и не наследует выделение соседнего текста.");
            StringAssert.Contains("<link=https://example.com>", text,
                "Ссылка сохраняет адрес перехода после оформления соседнего текста.");
            Label heading = renderer.ContentRoot.Q<Label>(className: "h1");
            Assert.That(heading, Is.Not.Null, "Заголовок сохраняет свой семантический класс.");
            Assert.That(heading.text, Is.EqualTo("Заголовок"));
        }

        [Test]
        public void SetMarkdown_MarksLastRenderedElementForUssSelectors()
        {
            global::UIMarkdownRenderer.UIMarkdownRenderer renderer = CreateRenderer();

            renderer.SetMarkdown("# Title\n\nBody");

            VisualElement lastDirectChild = renderer.ContentRoot.ElementAt(renderer.ContentRoot.childCount - 1);
            Assert.That(lastDirectChild.ClassListContains("markdown-last-child"), Is.True);

            Label bodyLabel = Labels(renderer.ContentRoot).Single(label => label.text == "Body");
            Assert.That(bodyLabel.ClassListContains("markdown-last-child"), Is.True);
        }

        [Test]
        public void OpenFile_WhenFileIsMissing_DoesNotThrowAndRendersFallback()
        {
            global::UIMarkdownRenderer.UIMarkdownRenderer renderer = CreateRenderer();
            string missingPath = Path.Combine(Path.GetTempPath(), $"missing-markdown-{Guid.NewGuid():N}.md");

            // OpenFile для отсутствующего файла больше НЕ логирует Debug.LogError (убран как избыточный) —
            // только мягко рендерит fallback. Поэтому LogAssert.Expect не нужен; проверяем сам fallback.
            Assert.DoesNotThrow(() => renderer.OpenFile(missingPath));

            string text = JoinedLabelText(renderer.ContentRoot);
            StringAssert.Contains("Markdown file not found", text);
            StringAssert.Contains(missingPath, text);
        }

        private static global::UIMarkdownRenderer.UIMarkdownRenderer CreateRenderer()
        {
            return new global::UIMarkdownRenderer.UIMarkdownRenderer(
                (_, _) => { },
                false,
                includeDefaultStylesheet: false);
        }

        private static VisualElement SingleElementWithClass(VisualElement root, string className)
        {
            List<VisualElement> elements = ElementsWithClass(root, className);
            Assert.That(elements, Has.Count.EqualTo(1));
            return elements[0];
        }

        private static List<VisualElement> ElementsWithClass(VisualElement root, string className)
        {
            List<VisualElement> elements = new();
            root.Query<VisualElement>(className: className).ForEach(elements.Add);
            return elements;
        }

        private static List<Label> Labels(VisualElement root)
        {
            List<Label> labels = new();
            root.Query<Label>().ForEach(labels.Add);
            return labels;
        }

        private static string JoinedLabelText(VisualElement root) => string.Join("\n", Labels(root).Select(label => label.text));
    }
}
