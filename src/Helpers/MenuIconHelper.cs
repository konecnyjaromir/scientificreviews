using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScientificReviews.Helpers
{
    public static class MenuIconHelper
    {
        private sealed class IconDefinition
        {
            public IconDefinition(string glyph, Color color)
            {
                Glyph = glyph;
                Color = color;
            }

            public string Glyph { get; }
            public Color Color { get; }
        }

        private static readonly Font IconFont = new Font("Segoe MDL2 Assets", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
        private static readonly Dictionary<string, Image> Icons = new Dictionary<string, Image>();

        public static void Apply(ToolStrip strip)
        {
            if (strip == null)
                return;

            MenuStrip menuStrip = strip as MenuStrip;
            if (menuStrip == null)
            {
                Apply(strip.Items);
                return;
            }

            menuStrip.ImageScalingSize = new Size(20, 20);
            foreach (ToolStripItem item in menuStrip.Items)
            {
                ToolStripMenuItem topLevelMenu = item as ToolStripMenuItem;
                if (topLevelMenu == null)
                    continue;

                string kind = GetTopLevelKind(topLevelMenu.Text);
                string key = "top:" + kind;
                if (!Icons.TryGetValue(key, out Image image))
                {
                    image = CreateTopLevelIcon(kind);
                    Icons.Add(key, image);
                }

                topLevelMenu.Image = image;
                topLevelMenu.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                Apply(topLevelMenu.DropDownItems);
            }
        }

        private static string GetTopLevelKind(string text)
        {
            string value = (text ?? string.Empty).Replace("&", string.Empty).Trim().ToLowerInvariant();
            if (value.Contains("project")) return "project";
            if (value.Contains("database")) return "database";
            if (value.Contains("pipeline")) return "pipelines";
            if (value.Contains("record")) return "record";
            if (value.Contains("copy")) return "copy";
            if (value.Contains("view")) return "view";
            if (value == "pdf") return "pdf";
            if (value.Contains("journal") || value.Contains("citation")) return "journals";
            if (value.Contains("about")) return "about";
            return "default";
        }

        private static Image CreateTopLevelIcon(string kind)
        {
            Bitmap bitmap = new Bitmap(24, 24);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Pen outline = new Pen(Color.FromArgb(55, 66, 82), 1.6f))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                outline.LineJoin = LineJoin.Round;

                switch (kind)
                {
                    case "project":
                        using (Brush folder = new SolidBrush(Color.FromArgb(255, 193, 75)))
                        using (Brush tab = new SolidBrush(Color.FromArgb(255, 215, 111)))
                        {
                            graphics.FillPolygon(folder, new[]
                            {
                                new Point(3, 7), new Point(9, 7), new Point(11, 9), new Point(21, 9),
                                new Point(21, 19), new Point(3, 19)
                            });
                            graphics.FillPolygon(tab, new[] { new Point(3, 9), new Point(21, 9), new Point(19, 13), new Point(4, 13) });
                        }
                        graphics.DrawLines(outline, new[] { new Point(3, 7), new Point(9, 7), new Point(11, 9), new Point(21, 9), new Point(21, 19), new Point(3, 19), new Point(3, 7) });
                        break;

                    case "database":
                        using (Brush blue = new SolidBrush(Color.FromArgb(77, 151, 220)))
                        {
                            graphics.FillRectangle(blue, 5, 6, 14, 12);
                            graphics.FillEllipse(blue, 5, 4, 14, 6);
                            graphics.FillEllipse(blue, 5, 15, 14, 6);
                        }
                        graphics.DrawArc(outline, 5, 4, 14, 6, 0, 180);
                        graphics.DrawArc(outline, 5, 15, 14, 6, 0, 180);
                        graphics.DrawLine(outline, 5, 7, 5, 18);
                        graphics.DrawLine(outline, 19, 7, 19, 18);
                        graphics.DrawArc(outline, 5, 9, 14, 6, 0, 180);
                        break;

                    case "pipelines":
                        using (Pen connector = new Pen(Color.FromArgb(116, 91, 190), 2f))
                        using (Brush node = new SolidBrush(Color.FromArgb(157, 125, 231)))
                        {
                            graphics.DrawLine(connector, 7, 6, 12, 12);
                            graphics.DrawLine(connector, 12, 12, 18, 6);
                            graphics.DrawLine(connector, 12, 12, 18, 18);
                            graphics.FillEllipse(node, 3, 3, 8, 7);
                            graphics.FillEllipse(node, 14, 3, 8, 7);
                            graphics.FillEllipse(node, 3, 15, 8, 7);
                            graphics.FillEllipse(node, 14, 15, 8, 7);
                        }
                        graphics.DrawEllipse(outline, 3, 3, 8, 7);
                        graphics.DrawEllipse(outline, 14, 3, 8, 7);
                        graphics.DrawEllipse(outline, 3, 15, 8, 7);
                        graphics.DrawEllipse(outline, 14, 15, 8, 7);
                        break;

                    case "record":
                        using (Brush page = new SolidBrush(Color.FromArgb(96, 190, 166)))
                        {
                            graphics.FillPolygon(page, new[] { new Point(3, 5), new Point(10, 7), new Point(12, 9), new Point(14, 7), new Point(21, 5), new Point(21, 19), new Point(14, 21), new Point(12, 19), new Point(10, 21), new Point(3, 19) });
                        }
                        graphics.DrawPolygon(outline, new[] { new Point(3, 5), new Point(10, 7), new Point(12, 9), new Point(14, 7), new Point(21, 5), new Point(21, 19), new Point(14, 21), new Point(12, 19), new Point(10, 21), new Point(3, 19) });
                        graphics.DrawLine(outline, 12, 9, 12, 19);
                        graphics.DrawLine(outline, 5, 9, 9, 10);
                        graphics.DrawLine(outline, 15, 10, 19, 9);
                        break;

                    case "copy":
                        using (Brush rear = new SolidBrush(Color.FromArgb(160, 191, 235)))
                        using (Brush front = new SolidBrush(Color.FromArgb(105, 160, 222)))
                        {
                            graphics.FillRectangle(rear, 4, 3, 12, 15);
                            graphics.FillRectangle(front, 8, 7, 12, 14);
                        }
                        graphics.DrawRectangle(outline, 4, 3, 12, 15);
                        graphics.DrawRectangle(outline, 8, 7, 12, 14);
                        graphics.DrawLine(outline, 11, 11, 17, 11);
                        graphics.DrawLine(outline, 11, 14, 17, 14);
                        break;

                    case "view":
                        using (Brush iris = new SolidBrush(Color.FromArgb(72, 157, 200)))
                        using (Brush pupil = new SolidBrush(Color.FromArgb(44, 82, 132)))
                        {
                            graphics.FillEllipse(iris, 3, 7, 18, 11);
                            graphics.FillEllipse(Brushes.White, 9, 6, 8, 12);
                            graphics.FillEllipse(pupil, 11, 9, 4, 6);
                        }
                        graphics.DrawArc(outline, 3, 7, 18, 11, 0, 180);
                        graphics.DrawArc(outline, 3, 7, 18, 11, 180, 180);
                        break;

                    case "pdf":
                        using (Brush sheet = new SolidBrush(Color.FromArgb(249, 250, 252)))
                        using (Brush red = new SolidBrush(Color.FromArgb(219, 85, 86)))
                        {
                            graphics.FillRectangle(sheet, 5, 3, 14, 18);
                            graphics.FillPolygon(red, new[] { new Point(5, 14), new Point(19, 14), new Point(19, 18), new Point(5, 18) });
                        }
                        graphics.DrawRectangle(outline, 5, 3, 14, 18);
                        graphics.DrawLine(outline, 14, 3, 19, 8);
                        graphics.DrawLine(outline, 14, 3, 14, 8);
                        graphics.DrawLine(outline, 14, 8, 19, 8);
                        using (Font font = new Font("Segoe UI", 5.5f, FontStyle.Bold, GraphicsUnit.Point))
                            TextRenderer.DrawText(graphics, "PDF", font, new Rectangle(5, 13, 14, 6), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                        break;

                    case "journals":
                        using (Pen chart = new Pen(Color.FromArgb(63, 158, 126), 2.2f))
                        using (Brush bar = new SolidBrush(Color.FromArgb(102, 162, 219)))
                        {
                            graphics.FillRectangle(bar, 4, 14, 4, 7);
                            graphics.FillRectangle(bar, 10, 10, 4, 11);
                            graphics.FillRectangle(bar, 16, 6, 4, 15);
                            graphics.DrawLines(chart, new[] { new Point(4, 11), new Point(9, 8), new Point(13, 9), new Point(19, 3) });
                        }
                        graphics.DrawLine(outline, 3, 21, 21, 21);
                        break;

                    case "about":
                        using (Brush blue = new SolidBrush(Color.FromArgb(85, 145, 205)))
                        using (Font font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            graphics.FillEllipse(blue, 3, 3, 18, 18);
                            TextRenderer.DrawText(graphics, "i", font, new Rectangle(3, 3, 18, 18), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                        }
                        break;

                    default:
                        using (Brush blue = new SolidBrush(Color.FromArgb(100, 145, 185)))
                            graphics.FillEllipse(blue, 4, 4, 16, 16);
                        graphics.DrawEllipse(outline, 4, 4, 16, 16);
                        break;
                }
            }

            return bitmap;
        }

        public static void Apply(ToolStripItemCollection items)
        {
            if (items == null)
                return;

            foreach (ToolStripItem item in items)
            {
                Apply(item);
            }
        }

        public static void Apply(ToolStripItem item)
        {
            ToolStripMenuItem menuItem = item as ToolStripMenuItem;
            if (menuItem == null)
                return;

            if (menuItem.Image == null)
            {
                IconDefinition icon = GetIcon(menuItem.Name + " " + menuItem.Text);
                string key = icon.Glyph + icon.Color.ToArgb();
                if (!Icons.TryGetValue(key, out Image image))
                {
                    image = CreateIcon(icon);
                    Icons.Add(key, image);
                }

                menuItem.Image = image;
            }

            menuItem.ImageScaling = ToolStripItemImageScaling.None;
            Apply(menuItem.DropDownItems);
        }

        private static Image CreateIcon(IconDefinition icon)
        {
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                TextRenderer.DrawText(
                    graphics,
                    icon.Glyph,
                    IconFont,
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    icon.Color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoClipping);
            }
            return bitmap;
        }

        private static IconDefinition GetIcon(string nameAndText)
        {
            string value = (nameAndText ?? string.Empty).ToLowerInvariant();

            if (Contains(value, "setting", "column", "mode", "option")) return new IconDefinition("\uE713", Color.FromArgb(105, 110, 125));
            if (Contains(value, "undo")) return new IconDefinition("\uE7A7", Color.FromArgb(55, 120, 190));
            if (Contains(value, "redo")) return new IconDefinition("\uE7A6", Color.FromArgb(55, 120, 190));
            if (Contains(value, "save")) return new IconDefinition("\uE74E", Color.FromArgb(40, 125, 75));
            if (Contains(value, "open", "load", "folder", "newtoolstripmenuitem", "clear archive")) return new IconDefinition("\uE8E5", Color.FromArgb(65, 125, 185));
            if (Contains(value, "search", "find")) return new IconDefinition("\uE721", Color.FromArgb(55, 120, 190));
            if (Contains(value, "copy", "duplicate")) return new IconDefinition("\uE8C8", Color.FromArgb(85, 105, 160));
            if (Contains(value, "cut")) return new IconDefinition("\uE8C6", Color.FromArgb(170, 100, 85));
            if (Contains(value, "paste")) return new IconDefinition("\uE77F", Color.FromArgb(85, 125, 160));
            if (Contains(value, "delete", "remove", "unbind", "exclude", "clear")) return new IconDefinition("\uE74D", Color.FromArgb(190, 75, 75));
            if (Contains(value, "flag", "green", "orange", "purple", "red")) return new IconDefinition("\uE7C1", Color.FromArgb(205, 145, 40));
            if (Contains(value, "pdf")) return new IconDefinition("\uE8A5", Color.FromArgb(185, 65, 65));
            if (Contains(value, "doi", "url", "google", "link")) return new IconDefinition("\uE71B", Color.FromArgb(55, 140, 150));
            if (Contains(value, "tag", "key", "rename")) return new IconDefinition("\uE8EC", Color.FromArgb(140, 100, 175));
            if (Contains(value, "edit")) return new IconDefinition("\uE70F", Color.FromArgb(70, 125, 180));
            if (Contains(value, "export")) return new IconDefinition("\uEDE1", Color.FromArgb(45, 135, 100));
            if (Contains(value, "import")) return new IconDefinition("\uE8E5", Color.FromArgb(65, 125, 185));
            if (Contains(value, "refresh", "update", "normalize", "autofix", "run", "fetch", "pair")) return new IconDefinition("\uE72C", Color.FromArgb(45, 140, 125));
            if (Contains(value, "journal", "database", "record", "project", "pipeline")) return new IconDefinition("\uE8F1", Color.FromArgb(70, 115, 165));
            if (Contains(value, "report", "about", "help")) return new IconDefinition("\uE946", Color.FromArgb(90, 125, 175));
            if (Contains(value, "filter", "exclude")) return new IconDefinition("\uE71C", Color.FromArgb(125, 105, 165));
            if (Contains(value, "add", "create", "insert")) return new IconDefinition("\uE710", Color.FromArgb(45, 145, 85));
            if (Contains(value, "refresh")) return new IconDefinition("\uE72C", Color.FromArgb(45, 140, 125));

            return new IconDefinition("\uE10C", Color.FromArgb(100, 115, 135));
        }

        private static bool Contains(string value, params string[] terms)
        {
            foreach (string term in terms)
            {
                if (value.Contains(term))
                    return true;
            }
            return false;
        }
    }
}
