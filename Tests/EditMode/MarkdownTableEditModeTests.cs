using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UIMarkdownRenderer.ObjectRenderers;
using UIMarkdownRenderer.ObjectRenderers.SyntaxHighlighting;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using MdRenderer = UIMarkdownRenderer.UIMarkdownRenderer;
using Object = UnityEngine.Object;

namespace Rtl.MarkdownRenderer.Tests.EditMode
{
    public class MarkdownTableEditModeTests
    {
        // Реалистичная таблица: 4 колонки, 6 строк (шапка + 5), одна длинная ячейка, встроенный код, ссылки,
        // пустая ячейка и все три вида выравнивания.
        private const string RealisticTable =
            "| Модуль | Тема | Что делает | Ссылка |\n" +
            "|:-------|:----:|------------|-------:|\n" +
            "| M1 | Основы | Короткая ячейка | [док](https://example.com/m1) |\n" +
            "| M2 | Циклы | Здесь намеренно длинная ячейка: текст должен переноситься внутри своей колонки и не " +
            "выталкивать соседние колонки за пределы родителя, сколько бы слов в ней ни было. | " +
            "[док](https://example.com/m2) |\n" +
            "| M3 | Код | Вызов `Debug.Log(\"x\")` в ячейке | нет |\n" +
            "| M4 | Пусто |  | [док](https://example.com/m4) |\n" +
            "| M5 | Коротко | ok | нет |\n";

        [Test]
        public void Table_BuildsRowsWithHeaderFlagAndCells()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown("| A | B | C |\n|---|---|---|\n| 1 | 2 | 3 |\n| 4 | 5 | 6 |\n| 7 | 8 | 9 |");

            VisualElement table = MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass);
            Assert.That(table.ClassListContains("block"), Is.True, "Таблица — обычный блок документа с его отступами.");
            Assert.That(table.parent, Is.SameAs(renderer.ContentRoot));

            List<VisualElement> rows = Rows(table);
            Assert.That(rows, Has.Count.EqualTo(4), "Шапка и три строки тела; строка-разделитель в дерево не попадает.");
            Assert.That(rows.All(row => row.parent == table), Is.True);
            Assert.That(rows.All(row => row.style.flexDirection.value == FlexDirection.Row), Is.True,
                "Ячейки строки стоят в ряд независимо от подключённой темы.");

            Assert.That(rows[0].ClassListContains(TableRenderer.HeaderRowClass), Is.True);
            Assert.That(rows.Skip(1).Any(row => row.ClassListContains(TableRenderer.HeaderRowClass)), Is.False,
                "Шапка только одна — первая строка.");

            Assert.That(rows.Select(row => row.ClassListContains(TableRenderer.AlternateRowClass)),
                Is.EqualTo(new[] { false, false, true, false }), "Полосатость — каждая вторая строка тела.");

            foreach (VisualElement row in rows)
            {
                Assert.That(Cells(row), Has.Count.EqualTo(3));
                Assert.That(row.ClassListContains("block"), Is.False, "Строка — не блок: отступы блока ей не нужны.");
            }

            Assert.That(rows.Select(row => string.Join(",", Cells(row).Select(MarkdownTestUtility.PlainLabelText))),
                Is.EqualTo(new[] { "A,B,C", "1,2,3", "4,5,6", "7,8,9" }));
        }

        [Test]
        public void Table_ColumnWeightsAreEqualAcrossRows()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown(
                "| N | Name | Description |\n|---|---|---|\n" +
                "| 1 | Alpha | A noticeably longer description of the first item |\n" +
                "| 2 | Beta gamma | Short |");

            List<VisualElement> rows = Rows(MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass));
            float[] header = Cells(rows[0]).Select(Weight).ToArray();

            foreach (VisualElement row in rows)
            {
                Assert.That(Cells(row).Select(Weight), Is.EqualTo(header),
                    "У ячеек одной колонки общий flex-grow — иначе колонки разъедутся между строками.");
                foreach (VisualElement cell in Cells(row))
                {
                    Assert.That(cell.style.flexBasis.value,
                        Is.EqualTo(new Length(Weight(cell) * TableRenderer.ColumnUnitWidth, LengthUnit.Pixel)),
                        "flex-basis пропорционален тому же весу: рост и сжатие делят ширину по весам, без измерения текста.");
                    Assert.That(cell.style.minWidth.value, Is.EqualTo(new Length(0f, LengthUnit.Pixel)));
                    Assert.That(cell.style.flexShrink.value, Is.EqualTo(1f));
                }
            }

            Assert.That(header[0], Is.EqualTo(TableRenderer.MinColumnWeight), "Короткая колонка зажата снизу.");
            Assert.That(header[1], Is.EqualTo(10f + TableRenderer.CellChromeWeight),
                "Вес — длина самого длинного содержимого колонки (Beta gamma) плюс добавка на отступы ячейки.");
            Assert.That(header[2], Is.EqualTo(TableRenderer.MaxColumnWeight), "Длинная колонка зажата сверху.");
        }

        [Test]
        public void Table_AppliesColumnAlignmentFromSeparatorRow()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown("| L | C | R | N |\n|:---|:---:|---:|---|\n| a | b | c | d |");

            List<VisualElement> rows = Rows(MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass));
            foreach (VisualElement row in rows)
            {
                List<VisualElement> cells = Cells(row);
                Assert.That(cells[0].Q<Label>().style.unityTextAlign.value, Is.EqualTo(TextAnchor.UpperLeft));
                Assert.That(cells[1].Q<Label>().style.unityTextAlign.value, Is.EqualTo(TextAnchor.UpperCenter));
                Assert.That(cells[2].Q<Label>().style.unityTextAlign.value, Is.EqualTo(TextAnchor.UpperRight));
                Assert.That(cells[3].Q<Label>().style.unityTextAlign.keyword, Is.EqualTo(StyleKeyword.Null),
                    "Колонка без двоеточий выравнивание не задаёт — его решает тема.");

                Assert.That(cells[0].ClassListContains(TableRenderer.CellAlignLeftClass), Is.True);
                Assert.That(cells[1].ClassListContains(TableRenderer.CellAlignCenterClass), Is.True);
                Assert.That(cells[2].ClassListContains(TableRenderer.CellAlignRightClass), Is.True);
                Assert.That(cells[3].GetClasses().Any(c => c.StartsWith(TableRenderer.CellClass + "--")), Is.False,
                    "У колонки без выравнивания нет класса-модификатора.");
            }
        }

        [Test]
        public void Table_KeepsInlineCodeLinkAndEmphasisInsideCells()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown(
                "| Что | Как |\n|---|---|\n" +
                "| Код | вызови `Run(a < b)` |\n" +
                "| Ссылка | [сайт](https://example.com/page) и **важно** |");

            List<VisualElement> rows = Rows(MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass));

            string code = MarkdownTestUtility.JoinedLabelText(Cells(rows[1])[1]);
            StringAssert.Contains($"<color={VsCodePalette.InlineCode}><b>Run(a <\u200B b)</b></color>", code,
                "Встроенный код в ячейке идёт через тот же рендер, что и в абзаце.");

            string link = MarkdownTestUtility.JoinedLabelText(Cells(rows[2])[1]);
            StringAssert.Contains("<link=https://example.com/page>", link, "Ссылка в ячейке сохраняет адрес.");
            StringAssert.Contains("сайт", link);
            StringAssert.Contains("<b>важно</b>", link);

            Label paragraph = Cells(rows[2])[1].Q<Label>(className: "paragraph");
            Assert.That(paragraph, Is.Not.Null, "Содержимое ячейки — обычный абзац конвейера.");
            Assert.That(paragraph.parent.ClassListContains("block"), Is.True);
        }

        [Test]
        public void Table_PadsShortRowsAndKeepsEmptyCells()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown("| A | B | C |\n|---|---|---|\n| 1 |\n| 4 |  | 6 |");

            List<VisualElement> rows = Rows(MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass));
            Assert.That(rows, Has.Count.EqualTo(3));
            foreach (VisualElement row in rows)
            {
                Assert.That(Cells(row), Has.Count.EqualTo(3), "Короткая строка дополняется пустыми ячейками.");
            }

            Assert.That(Cells(rows[1]).Select(MarkdownTestUtility.PlainLabelText), Is.EqualTo(new[] { "1", "", "" }));
            Assert.That(Cells(rows[2]).Select(MarkdownTestUtility.PlainLabelText), Is.EqualTo(new[] { "4", "", "6" }));
            Assert.That(Cells(rows[1]).Select(Weight), Is.EqualTo(Cells(rows[0]).Select(Weight)),
                "Дополненные ячейки получают вес своей колонки.");
        }

        [Test]
        public void PipeTextWithoutSeparatorRow_StaysParagraph()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown(
                "Выбери a | b | c, а не d.\n\n" +
                "| просто | строка |\n\n" +
                "**жирный | текст** и `код | с чертой`\n\n" +
                "первая | строка\nвторая | строка");

            Assert.That(MarkdownTestUtility.ElementsWithClass(renderer.ContentRoot, TableRenderer.TableClass), Is.Empty,
                "Без строки-разделителя под шапкой таблицы нет.");

            List<Label> paragraphs = MarkdownTestUtility.ElementsWithClass(renderer.ContentRoot, "paragraph").Cast<Label>().ToList();
            Assert.That(paragraphs, Has.Count.EqualTo(4));
            Assert.That(MarkdownTestUtility.PlainText(paragraphs[0].text), Is.EqualTo("Выбери a | b | c, а не d."),
                "Символы | остаются в тексте абзаца.");
            Assert.That(MarkdownTestUtility.PlainText(paragraphs[1].text), Is.EqualTo("| просто | строка |"));
            Assert.That(MarkdownTestUtility.PlainText(paragraphs[2].text), Is.EqualTo("жирный | текст и код | с чертой"));
            StringAssert.Contains("<b>жирный | текст</b>", paragraphs[2].text, "Выделение поверх | не ломается.");
            Assert.That(MarkdownTestUtility.PlainText(paragraphs[3].text), Is.EqualTo("первая | строка вторая | строка"));
        }

        [Test]
        public void Table_InsideList_FallsBackToTextWithoutLosingContent()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            Assert.DoesNotThrow(() => renderer.SetMarkdown(
                "- Пункт с таблицей\n\n" +
                "  | Ключ | Значение |\n" +
                "  |------|----------|\n" +
                "  | режим | `fast` |\n" +
                "  | сайт | [док](https://example.com) |\n\n" +
                "- Следующий пункт"));

            Assert.That(MarkdownTestUtility.ElementsWithClass(renderer.ContentRoot, TableRenderer.TableClass), Is.Empty,
                "Внутри списка дерево таблицы не строится — список пишется одной меткой.");

            Label list = MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, "list") as Label;
            Assert.That(list, Is.Not.Null);
            string plain = MarkdownTestUtility.PlainText(list.text);
            foreach (string expected in new[] { "Пункт с таблицей", "Ключ | Значение", "режим | fast", "сайт | док", "Следующий пункт" })
            {
                StringAssert.Contains(expected, plain, "Содержимое таблицы в списке остаётся читаемым текстом.");
            }

            StringAssert.Contains("<link=https://example.com>", list.text);
            StringAssert.Contains($"<color={VsCodePalette.InlineCode}><b>fast</b></color>", list.text);
        }

        [Test]
        public void Table_SyntaxVariants_AreRecognised()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown(
                "A | B\n---|---\n1 | 2\n\n" +
                "| Только | шапка |\n|---|---|\n\n" +
                "| Черта | Текст |\n|---|---|\n| a \\| b | c |\n\n" +
                "Строка текста прямо над таблицей\n| X | Y |\n|---|---|\n| 3 | 4 |");

            List<VisualElement> tables = MarkdownTestUtility.ElementsWithClass(renderer.ContentRoot, TableRenderer.TableClass);
            Assert.That(tables, Has.Count.EqualTo(3), "Таблице нужна пустая строка перед шапкой: четвёртый блок — абзац.");

            Assert.That(Rows(tables[0]).Select(row => string.Join(",", Cells(row).Select(MarkdownTestUtility.PlainLabelText))),
                Is.EqualTo(new[] { "A,B", "1,2" }), "Таблица без крайних | распознаётся.");

            Assert.That(Rows(tables[1]), Has.Count.EqualTo(1), "Таблица из одной шапки допустима.");
            Assert.That(Rows(tables[1])[0].ClassListContains(TableRenderer.HeaderRowClass), Is.True);

            Assert.That(Cells(Rows(tables[2])[1]).Select(MarkdownTestUtility.PlainLabelText), Is.EqualTo(new[] { "a | b", "c" }),
                "Экранированный \\| остаётся символом в ячейке, а не делит её.");

            string tail = MarkdownTestUtility.PlainText(MarkdownTestUtility.Labels(renderer.ContentRoot).Last().text);
            foreach (string expected in new[] { "Строка текста прямо над таблицей", "X", "Y", "3", "4" })
            {
                StringAssert.Contains(expected, tail, "Без пустой строки таблица остаётся текстом, но ничего не теряется.");
            }
        }

        [UnityTest]
        public IEnumerator Table_ImageInsideCell_LoadsIntoTheCell()
        {
            string folder = Path.Combine(Path.GetTempPath(), $"markdown-table-image-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            Texture2D source = new(4, 2);
            File.WriteAllBytes(Path.Combine(folder, "pic.png"), source.EncodeToPNG());
            Object.DestroyImmediate(source);

            try
            {
                MdRenderer renderer = MarkdownTestUtility.CreateRenderer();
                renderer.SetFileContext(Path.Combine(folder, "doc.md"));
                renderer.SetMarkdown("| Картинка | Подпись |\n|---|---|\n| ![схема](./pic.png) | текст |\n\nПосле таблицы.");

                VisualElement table = MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass);
                List<VisualElement> bodyCells = Cells(Rows(table)[1]);
                Assert.That(bodyCells, Has.Count.EqualTo(2));

                Image image = bodyCells[0].Q<Image>(className: "md-image");
                Assert.That(image, Is.Not.Null, "Картинка из ячейки остаётся внутри своей ячейки.");
                Assert.That(image.tooltip, Is.EqualTo("схема"));
                Assert.That(MarkdownTestUtility.PlainLabelText(bodyCells[1]), Is.EqualTo("текст"));
                Assert.That(MarkdownTestUtility.Labels(renderer.ContentRoot).Last().parent.parent, Is.SameAs(renderer.ContentRoot),
                    "Картинка в ячейке не ломает стек блоков: абзац после таблицы лежит в корне.");

                Texture placeholder = image.image;
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 10d;
                while (image.image == placeholder && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(image.image, Is.Not.SameAs(placeholder), "Относительная картинка загрузилась из папки файла.");
                Assert.That(image.image.width, Is.EqualTo(4));
                Assert.That(image.image.height, Is.EqualTo(2));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TablesExample_RendersEveryTable()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MdRenderer).Assembly);
            if (package == null)
            {
                Assert.Ignore("Пакет встроен в проект не как UPM-пакет — путь к Examples неизвестен.");
            }

            string path = Path.Combine(package.resolvedPath, "Examples", "04_Tables.md");
            Assert.That(File.Exists(path), Is.True, path);

            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();
            Assert.DoesNotThrow(() => renderer.OpenFile(path));

            List<VisualElement> tables = MarkdownTestUtility.ElementsWithClass(renderer.ContentRoot, TableRenderer.TableClass);
            Assert.That(tables, Has.Count.EqualTo(5), "Пять таблиц примера строятся деревом; шестая — внутри списка — текстом.");
            foreach (VisualElement table in tables)
            {
                List<VisualElement> rows = Rows(table);
                int columns = Cells(rows[0]).Count;
                Assert.That(rows.All(row => Cells(row).Count == columns), Is.True, "В каждой строке одинаковое число ячеек.");
            }

            StringAssert.Contains("mode | fast", MarkdownTestUtility.PlainLabelText(renderer.ContentRoot));
            StringAssert.Contains("left | middle | right", MarkdownTestUtility.PlainLabelText(renderer.ContentRoot));
        }

        [Test]
        public void RealisticTable_StyleValuesKeepColumnsAlignedAndTextWrapping()
        {
            MdRenderer renderer = MarkdownTestUtility.CreateRenderer();

            renderer.SetMarkdown("# Таблица\n\n" + RealisticTable + "\nАбзац после таблицы.");

            VisualElement table = MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass);
            List<VisualElement> rows = Rows(table);
            Assert.That(rows, Has.Count.EqualTo(6));

            float[] weights = Cells(rows[0]).Select(Weight).ToArray();
            Assert.That(weights, Has.Length.EqualTo(4));
            foreach (VisualElement row in rows)
            {
                Assert.That(Cells(row), Has.Count.EqualTo(4), "В каждой строке одинаковое число ячеек.");
                Assert.That(Cells(row).Select(Weight), Is.EqualTo(weights), "Колонка делит один flex-grow на все строки.");
            }

            Assert.That(weights[2], Is.GreaterThan(weights[0]), "Колонка с длинным текстом шире коротких.");
            Assert.That(weights.All(w => w >= TableRenderer.MinColumnWeight && w <= TableRenderer.MaxColumnWeight), Is.True);

            List<Label> labels = MarkdownTestUtility.Labels(table);
            Assert.That(labels, Is.Not.Empty);
            foreach (Label label in labels)
            {
                Assert.That(label.style.whiteSpace.keyword, Is.EqualTo(StyleKeyword.Undefined));
                Assert.That(label.style.whiteSpace.value, Is.EqualTo(WhiteSpace.Normal),
                    "Ни одна ячейка не запрещает перенос текста.");
            }

            StringAssert.Contains("Debug.Log(\"x\")", MarkdownTestUtility.PlainLabelText(Cells(rows[3])[2]));
            StringAssert.Contains("<link=https://example.com/m2>", MarkdownTestUtility.JoinedLabelText(Cells(rows[2])[3]));
            Assert.That(MarkdownTestUtility.PlainLabelText(Cells(rows[4])[2]), Is.Empty, "Пустая ячейка остаётся ячейкой.");

            Debug.Log("[MarkdownTable] style facts: rows=" + rows.Count + ", cells per row=" +
                      string.Join(",", rows.Select(row => Cells(row).Count)) + ", column flex-grow=" +
                      string.Join(" / ", weights) + ", labels=" + labels.Count + ", nowrap labels=0");

            Label after = MarkdownTestUtility.Labels(renderer.ContentRoot).Last();
            Assert.That(after.text, Is.EqualTo("Абзац после таблицы."), "Текст после таблицы пишется в свой абзац.");
            Assert.That(after.parent.parent, Is.SameAs(renderer.ContentRoot), "После таблицы стек блоков сбалансирован.");
        }

        [Test]
        public void RealisticTable_LaidOutInPanel_ColumnsLineUpAndTableFitsParent()
        {
            const float hostWidth = 520f;
            using PanelHost panel = PanelHost.TryCreate(hostWidth);
            if (panel == null)
            {
                Assert.Ignore("Панель UI Toolkit без окна редактора создать не удалось — раскладка проверена только " +
                              "по значениям стилей (RealisticTable_StyleValuesKeepColumnsAlignedAndTextWrapping).");
            }

            MdRenderer renderer = MarkdownTestUtility.CreateRenderer(true);
            renderer.SetMarkdown(RealisticTable);
            panel.Host.Add(renderer.RootElement);

            if (!panel.Layout())
            {
                Assert.Ignore("Раскладку панели принудительно пересчитать не удалось — проверены только значения стилей.");
            }

            VisualElement table = MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass);
            List<VisualElement> rows = Rows(table);
            const float tolerance = 0.51f;

            Assert.That(panel.Host.layout.width, Is.EqualTo(hostWidth).Within(tolerance));
            Assert.That(table.worldBound.xMin, Is.GreaterThanOrEqualTo(panel.Host.worldBound.xMin - tolerance));
            Assert.That(table.worldBound.xMax, Is.LessThanOrEqualTo(panel.Host.worldBound.xMax + tolerance),
                "Таблица не шире родителя.");
            Assert.That(table.layout.width, Is.GreaterThan(hostWidth * 0.8f), "Таблица занимает ширину родителя.");

            float[] columnLeft = Cells(rows[0]).Select(cell => cell.worldBound.xMin).ToArray();
            float[] columnWidth = Cells(rows[0]).Select(cell => cell.worldBound.width).ToArray();
            foreach (VisualElement row in rows)
            {
                Assert.That(row.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row));
                List<VisualElement> cells = Cells(row);
                for (int column = 0; column < cells.Count; column++)
                {
                    Rect bound = cells[column].worldBound;
                    Assert.That(bound.xMin, Is.EqualTo(columnLeft[column]).Within(tolerance),
                        $"Колонка {column} начинается в одном месте во всех строках.");
                    Assert.That(bound.width, Is.EqualTo(columnWidth[column]).Within(tolerance),
                        $"Колонка {column} одной ширины во всех строках.");
                    Assert.That(bound.width, Is.GreaterThan(20f), "Колонка не схлопнулась.");
                    Assert.That(bound.yMin, Is.EqualTo(row.worldBound.yMin).Within(tolerance));
                    Assert.That(bound.height, Is.EqualTo(row.worldBound.height).Within(tolerance),
                        "Ячейки строки одной высоты — вертикальные линии сетки непрерывны.");

                    foreach (Label label in MarkdownTestUtility.Labels(cells[column]))
                    {
                        Assert.That(label.resolvedStyle.whiteSpace, Is.EqualTo(WhiteSpace.Normal));
                        Assert.That(label.worldBound.xMin, Is.GreaterThanOrEqualTo(bound.xMin - tolerance));
                        Assert.That(label.worldBound.xMax, Is.LessThanOrEqualTo(bound.xMax + tolerance),
                            "Текст ячейки не выходит за её границы.");
                    }
                }

                Assert.That(cells.Last().worldBound.xMax, Is.LessThanOrEqualTo(table.worldBound.xMax + tolerance));
            }

            Assert.That(columnWidth[2], Is.GreaterThan(columnWidth[0]), "Колонка с длинным текстом шире коротких.");

            // Метка выровненной колонки растянута на ячейку — иначе -unity-text-align не на что влиять.
            VisualElement rightCell = Cells(rows[1])[3];
            Label rightLabel = rightCell.Q<Label>();
            Assert.That(rightLabel.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperRight));
            float rightContentWidth = rightCell.layout.width - rightCell.resolvedStyle.paddingLeft -
                                      rightCell.resolvedStyle.paddingRight - rightCell.resolvedStyle.borderLeftWidth -
                                      rightCell.resolvedStyle.borderRightWidth;

            Label shortLabel = Cells(rows[1])[2].Q<Label>();
            Label longLabel = Cells(rows[2])[2].Q<Label>();
            Debug.Log("[MarkdownTable] layout facts: host=" + panel.Host.layout.width + ", table=" + table.layout.width +
                      ", column widths=" + string.Join(" / ", columnWidth.Select(w => w.ToString("0.0"))) +
                      ", row heights=" + string.Join(" / ", rows.Select(r => r.layout.height.ToString("0.0"))) +
                      ", short label height=" + shortLabel.layout.height.ToString("0.0") +
                      ", long label height=" + longLabel.layout.height.ToString("0.0") +
                      ", cell padding L/R=" + rightCell.resolvedStyle.paddingLeft + "/" + rightCell.resolvedStyle.paddingRight +
                      ", cell border R/B=" + rightCell.resolvedStyle.borderRightWidth + "/" + rightCell.resolvedStyle.borderBottomWidth);

            float rightLabelMargins = rightLabel.resolvedStyle.marginLeft + rightLabel.resolvedStyle.marginRight;
            Assert.That(rightLabel.parent.layout.width, Is.EqualTo(rightContentWidth).Within(1f));
            Assert.That(rightLabel.layout.width + rightLabelMargins, Is.EqualTo(rightContentWidth).Within(1f),
                "Метка занимает всю ширину ячейки, поэтому выравнивание по правому краю видно.");

            Assert.That(rows[0].layout.height, Is.EqualTo(rows[1].layout.height).Within(tolerance),
                "Короткие заголовки помещаются в свои колонки в одну строку — как и короткие ячейки тела.");

            if (shortLabel.layout.height > 0f)
            {
                Assert.That(longLabel.layout.height, Is.GreaterThan(shortLabel.layout.height * 1.5f),
                    "Длинный текст переносится на несколько строк, а не растягивает колонку.");
                Assert.That(rows[2].layout.height, Is.GreaterThan(rows[1].layout.height),
                    "Строка с длинной ячейкой выше остальных.");
            }
        }

        [Test]
        public void WideTable_LaidOutInNarrowPanel_DoesNotOverflowParent()
        {
            const float hostWidth = 260f;
            using PanelHost panel = PanelHost.TryCreate(hostWidth);
            if (panel == null)
            {
                Assert.Ignore("Панель UI Toolkit без окна редактора создать не удалось.");
            }

            string header = "| " + string.Join(" | ", Enumerable.Range(1, 8).Select(i => "Колонка " + i)) + " |\n";
            string separator = "|" + string.Concat(Enumerable.Repeat("---|", 8)) + "\n";
            string body = "| " + string.Join(" | ", Enumerable.Range(1, 8).Select(i => "значение номер " + i)) + " |\n";

            MdRenderer renderer = MarkdownTestUtility.CreateRenderer(true);
            renderer.SetMarkdown(header + separator + body + body);
            panel.Host.Add(renderer.RootElement);

            if (!panel.Layout())
            {
                Assert.Ignore("Раскладку панели принудительно пересчитать не удалось.");
            }

            VisualElement table = MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass);
            Assert.That(table.worldBound.xMax, Is.LessThanOrEqualTo(panel.Host.worldBound.xMax + 0.51f),
                "Широкая таблица не вылезает за родителя по горизонтали.");

            foreach (VisualElement row in Rows(table))
            {
                List<VisualElement> cells = Cells(row);
                Assert.That(cells, Has.Count.EqualTo(8));
                Assert.That(cells.Last().worldBound.xMax, Is.LessThanOrEqualTo(table.worldBound.xMax + 0.51f));
                foreach (VisualElement cell in cells)
                {
                    Assert.That(cell.layout.width, Is.GreaterThan(0f));
                    foreach (Label label in MarkdownTestUtility.Labels(cell))
                    {
                        Assert.That(label.worldBound.xMax, Is.LessThanOrEqualTo(cell.worldBound.xMax + 0.51f),
                            "Метка сжимается до узкой колонки и переносит текст, а не наезжает на соседнюю ячейку.");
                    }
                }
            }

            Debug.Log("[MarkdownTable] wide table: host=" + panel.Host.layout.width + ", table=" + table.layout.width +
                      ", cell width=" + Cells(Rows(table)[0])[0].layout.width.ToString("0.0"));
        }

        [Test]
        public void Table_InShrinkWrappedParent_DoesNotCollapse()
        {
            // Родитель без заданной ширины (пузырь сообщения, который обжимает содержимое). При flex-basis: 0 у
            // ячеек таблица здесь схлопывалась до 27px — по отступам ячеек; база по весу даёт ей свою ширину.
            using PanelHost panel = PanelHost.TryCreate(900f);
            if (panel == null)
            {
                Assert.Ignore("Панель UI Toolkit без окна редактора создать не удалось.");
            }

            VisualElement bubble = new() { name = "shrink-wrapped-bubble" };
            bubble.style.alignSelf = Align.FlexStart;
            bubble.style.maxWidth = 420f;
            panel.Host.Add(bubble);

            MdRenderer renderer = MarkdownTestUtility.CreateRenderer(true);
            renderer.SetMarkdown("| Ключ | Значение |\n|---|---|\n| режим | быстрый |\n| попытки | три |");
            bubble.Add(renderer.RootElement);

            if (!panel.Layout())
            {
                Assert.Ignore("Раскладку панели принудительно пересчитать не удалось.");
            }

            VisualElement table = MarkdownTestUtility.SingleElementWithClass(renderer.ContentRoot, TableRenderer.TableClass);
            List<VisualElement> rows = Rows(table);
            Label widest = MarkdownTestUtility.Labels(table).OrderByDescending(label => label.layout.width).First();
            Debug.Log("[MarkdownTable] shrink-wrapped parent: bubble=" + bubble.layout.width + ", table=" + table.layout.width +
                      ", cells=" + string.Join(" / ", Cells(rows[0]).Select(cell => cell.layout.width.ToString("0.0"))) +
                      ", row heights=" + string.Join(" / ", rows.Select(row => row.layout.height.ToString("0.0"))) +
                      ", widest label=" + widest.layout.width.ToString("0.0"));

            Assert.That(bubble.layout.width, Is.LessThanOrEqualTo(420.51f));
            Assert.That(table.layout.width, Is.GreaterThan(100f), "Таблица получила ширину по содержимому, а не схлопнулась.");
            Assert.That(rows.Select(row => row.layout.height).Distinct().Count(), Is.EqualTo(1),
                "Короткие ячейки остаются в одну строку.");
        }

        private static List<VisualElement> Rows(VisualElement table) =>
            table.Children().Where(child => child.ClassListContains(TableRenderer.RowClass)).ToList();

        private static List<VisualElement> Cells(VisualElement row) =>
            row.Children().Where(child => child.ClassListContains(TableRenderer.CellClass)).ToList();

        private static float Weight(VisualElement cell)
        {
            Assert.That(cell.style.flexGrow.keyword, Is.EqualTo(StyleKeyword.Undefined), "flex-grow ячейки задан инлайном.");
            return cell.style.flexGrow.value;
        }

        /// <summary>
        /// Панель UI Toolkit без окна редактора: рантайм-панель <see cref="UIDocument" /> c контейнером
        /// фиксированной ширины. Раскладка пересчитывается принудительно (внутренний ValidateLayout панели),
        /// поэтому тест не ждёт кадра и ничего не показывает на экране.
        /// </summary>
        private sealed class PanelHost : IDisposable
        {
            private GameObject m_GameObject;
            private PanelSettings m_Settings;

            public VisualElement Host { get; private set; }

            public static PanelHost TryCreate(float width)
            {
                PanelHost result = new();
                // У панели без темы Unity пишет предупреждение об отсутствии Theme Style Sheet — тесту оно не мешает.
                bool ignoreFailingMessages = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    result.m_Settings = ScriptableObject.CreateInstance<PanelSettings>();
                    result.m_GameObject = new GameObject("MarkdownTableLayoutTest") { hideFlags = HideFlags.HideAndDontSave };
                    UIDocument document = result.m_GameObject.AddComponent<UIDocument>();
                    document.panelSettings = result.m_Settings;

                    VisualElement root = document.rootVisualElement;
                    if (root == null || root.panel == null)
                    {
                        result.Dispose();
                        return null;
                    }

                    VisualElement host = new() { name = "markdown-table-layout-host" };
                    host.style.position = Position.Absolute;
                    host.style.left = 0f;
                    host.style.top = 0f;
                    host.style.width = width;
                    host.style.fontSize = 12f;
                    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font != null)
                    {
                        // У панели без темы шрифта нет; без него текст не измеряется и перенос не проверить.
                        host.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
                    }

                    root.Add(host);
                    result.Host = host;
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MarkdownTable] panel host is not available: " + ex.Message);
                    result.Dispose();
                    return null;
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = ignoreFailingMessages;
                }
            }

            public bool Layout()
            {
                IPanel panel = Host?.panel;
                if (panel == null)
                {
                    return false;
                }

                MethodInfo validate = panel.GetType().GetMethod("ValidateLayout",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (validate == null)
                {
                    return false;
                }

                bool ignoreFailingMessages = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    validate.Invoke(panel, null);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MarkdownTable] panel layout failed: " + ex);
                    return false;
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = ignoreFailingMessages;
                }

                return !float.IsNaN(Host.layout.width) && Host.layout.width > 0f;
            }

            public void Dispose()
            {
                Host?.RemoveFromHierarchy();
                if (m_GameObject != null)
                {
                    Object.DestroyImmediate(m_GameObject);
                }

                if (m_Settings != null)
                {
                    Object.DestroyImmediate(m_Settings);
                }
            }
        }
    }
}
