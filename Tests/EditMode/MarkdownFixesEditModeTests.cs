using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UIMarkdownRenderer.ObjectRenderers.SyntaxHighlighting;
using UnityEngine;
using UnityEngine.UIElements;
using MdRenderer = UIMarkdownRenderer.UIMarkdownRenderer;
using Object = UnityEngine.Object;

namespace Rtl.MarkdownRenderer.Tests.EditMode
{
    /// <summary>Исправления 1.3.0, не относящиеся к таблицам: списки, код в списке, контекст файла, стили.</summary>
    public class MarkdownFixesEditModeTests
    {
        [Test]
        public void OrderedList_StartsFromAuthoredNumber()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown("3. три\n4. четыре\n5. пять");

            string text = ((Label)MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, "list")).text;
            Assert.That(text, Is.EqualTo("3. три\n4. четыре\n5. пять\n"), "Список нумеруется от числа автора, а не от 1.");
        }

        [Test]
        public void OrderedList_ContinuedAfterInterruptingBlockKeepsItsNumber()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown("1. один\n2. два\n\nАбзац между списками.\n\n3. три\n4. четыре\n\n- маркер");

            List<Label> lists = MarkdownTestUtility.ElementsWithClass(renderer.ContentRoot, "list").OfType<Label>().ToList();
            Assert.That(lists, Has.Count.EqualTo(3));
            Assert.That(lists[0].text, Is.EqualTo("1. один\n2. два\n"));
            Assert.That(lists[1].text, Is.EqualTo("3. три\n4. четыре\n"),
                "Список, продолженный после абзаца, сохраняет написанный автором номер.");
            Assert.That(lists[2].text, Is.EqualTo("\u2022 маркер\n"), "Маркированный список не затронут.");
        }

        [Test]
        public void FencedCodeBlockInsideListItem_IsNotDropped()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            Assert.DoesNotThrow(() => renderer.SetMarkdown(
                "1. Напиши функцию:\n\n" +
                "   ```python\n" +
                "   def area(r):\n" +
                "       return 3 * r\n" +
                "   ```\n\n" +
                "2. Сравни значения:\n\n" +
                "   ```\n" +
                "   a < b & c\n" +
                "   ```\n\n" +
                "3. Готово"));

            Label list = MarkdownTestUtility.ElementsWithClass(renderer.ContentRoot, "list").OfType<Label>().Single();
            string plain = MarkdownTestUtility.PlainText(list.text);

            StringAssert.Contains("1. Напиши функцию:", plain);
            StringAssert.Contains("def area(r):", plain, "Код из пункта списка раньше молча пропадал.");
            StringAssert.Contains("    return 3 * r", plain, "Отступы внутри кода сохраняются.");
            StringAssert.Contains("a < b & c", plain, "Код без языка тоже остаётся, с экранированием rich text.");
            StringAssert.Contains("a <\u200B b &\u200B c", list.text);
            StringAssert.Contains("2. Сравни значения:", plain);
            StringAssert.Contains("3. Готово", plain);

            SyntaxPalette palette = SyntaxPalette.Load("python");
            StringAssert.Contains($"<color={palette.Keyword}>def</color>", list.text, "Известный язык подсвечивается и в списке.");

            int codeIndex = plain.IndexOf("def area(r):", StringComparison.Ordinal);
            Assert.That(codeIndex, Is.GreaterThan(plain.IndexOf("1. Напиши функцию:", StringComparison.Ordinal)));
            Assert.That(codeIndex, Is.LessThan(plain.IndexOf("2. Сравни значения:", StringComparison.Ordinal)),
                "Код стоит под своим пунктом.");
            Assert.That(plain.Split('\n'), Does.Contain("  def area(r):"), "Каждая строка кода — отдельная строка метки.");
        }

        [Test]
        public void FencedCodeBlockOutsideList_StillRendersAsCodeBlock()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown("- пункт\n\n```\nкод вне списка\n```");

            VisualElement codeBlock = MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, "codeblock");
            StringAssert.Contains("код вне списка", MarkdownTestUtility.JoinedLabelText(codeBlock));
        }

        [Test]
        public void SetFileContext_SetsFolderForRelativeLinksWithoutRendering()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();
            string folder = Path.Combine(Path.GetTempPath(), $"markdown-context-{Guid.NewGuid():N}", "lessons");
            string file = Path.Combine(folder, "lesson.md");

            renderer.SetFileContext(file);

            Assert.That(renderer.FileFolder, Is.EqualTo(Path.GetFullPath(folder)), "Та же папка, что выставил бы OpenFile.");
            Assert.That(renderer.ContentRoot.childCount, Is.Zero, "SetFileContext ничего не рендерит.");

            string expected = Path.GetFullPath(Path.Combine(folder, "img", "pic.png")).Replace("\\", "/");
            Assert.That(renderer.ResolveLink("./img/pic.png"), Is.EqualTo(expected));

            string parent = Path.GetFullPath(Path.Combine(folder, "..", "shared", "pic.png")).Replace("\\", "/");
            Assert.That(renderer.ResolveLink("../shared/pic.png"), Is.EqualTo(parent),
                "Путь нормализован: в нём не остаётся сегментов \"..\".");
            StringAssert.DoesNotContain("..", renderer.ResolveLink("../shared/pic.png"));

            Assert.That(renderer.ResolveLink("https://example.com/a.png"), Is.EqualTo("https://example.com/a.png"));

            renderer.SetFileContext(string.Empty);
            renderer.SetFileContext(null);
            Assert.That(renderer.FileFolder, Is.EqualTo(Path.GetFullPath(folder)), "Пустой путь контекст не сбрасывает.");

            renderer.SetMarkdown("Текст после контекста.");
            Assert.That(renderer.FileFolder, Is.EqualTo(Path.GetFullPath(folder)), "SetMarkdown контекст не трогает.");
        }

        [Test]
        public void OpenFile_WithEmptyPath_DoesNotThrowAndRendersFallback()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            Assert.DoesNotThrow(() => renderer.OpenFile(string.Empty));
            StringAssert.Contains("Markdown file not found", MarkdownTestUtility.JoinedLabelText(renderer.ContentRoot));

            Assert.DoesNotThrow(() => renderer.OpenFile(null));
            Assert.That(renderer.FileFolder, Is.Null.Or.Empty, "Пустой путь контекст файла не выставляет.");
        }

        [Test]
        public void YamlFrontMatter_WithMalformedLines_DoesNotBreakRender()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            Assert.DoesNotThrow(() => renderer.SetMarkdown("---\nuss\ntitle: Demo: with colon\nuss:\n---\n\n# Заголовок\n\nТекст."));

            Label heading = renderer.ContentRoot.Q<Label>(className: "h1");
            Assert.That(heading, Is.Not.Null, "Строка front matter без двоеточия раньше роняла рендер документа.");
            Assert.That(heading.text, Is.EqualTo("Заголовок"));
            StringAssert.Contains("Текст.", MarkdownTestUtility.JoinedLabelText(renderer.ContentRoot));
        }

        [Test]
        public void ExplicitStylesheet_AppliesToItsOwnRendererOnly()
        {
            StyleSheet first = ScriptableObject.CreateInstance<StyleSheet>();
            StyleSheet second = ScriptableObject.CreateInstance<StyleSheet>();
            try
            {
                MdRenderer withFirst = new((_, _) => { }, false, first);
                MdRenderer withSecond = new((_, _) => { }, false, second);
                MdRenderer withDefault = new((_, _) => { }, false);
                MdRenderer without = MarkdownTestUtility.CreateRenderer();

                Assert.That(withFirst.RootElement.styleSheets.Contains(first), Is.True);
                Assert.That(withFirst.RootElement.styleSheets.Contains(second), Is.False,
                    "Стиль, переданный другому рендереру, на этот не действует.");
                Assert.That(withSecond.RootElement.styleSheets.Contains(second), Is.True);
                Assert.That(withSecond.RootElement.styleSheets.Contains(first), Is.False);

                Assert.That(withDefault.RootElement.styleSheets.count, Is.EqualTo(1), "Без аргумента — стиль пакета.");
                Assert.That(withDefault.RootElement.styleSheets.Contains(first), Is.False,
                    "Явно переданный стиль больше не становится стилем по умолчанию для всех.");
                Assert.That(withDefault.RootElement.styleSheets.Contains(second), Is.False);
                StringAssert.EndsWith("Styles/MarkdownRenderer.uss",
                    UnityEditor.AssetDatabase.GetAssetPath(withDefault.RootElement.styleSheets[0]));

                Assert.That(without.RootElement.styleSheets.count, Is.Zero, "includeDefaultStylesheet: false по-прежнему без стиля.");
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void VideoLink_ExtensionCheckIsCaseInsensitive()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            Assert.DoesNotThrow(() => renderer.SetMarkdown("![ролик](clip.MP4)"));

            List<VideoPlayerElement> players = new();
            renderer.ContentRoot.Query<VideoPlayerElement>().ForEach(players.Add);
            Assert.That(players, Has.Count.EqualTo(1), "\".MP4\" — тоже видео, а не картинка.");
            Assert.That(MarkdownTestUtility.ElementsWithClass(renderer.ContentRoot, "md-image"), Is.Empty);
            Assert.That(players[0].tooltip, Is.EqualTo("ролик"));
        }
    }
}
