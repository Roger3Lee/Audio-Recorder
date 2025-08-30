using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;

namespace AudioRecorder
{
    /// <summary>
    /// SVG图标转换器 - 为WPF提供协调的图标支持
    /// </summary>
    public static class SvgIconConverter
    {
        /// <summary>
        /// 创建高质量图标
        /// </summary>
        public static BitmapSource CreateIcon(string iconName, int width, int height)
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var center = new WpfPoint(width / 2.0, height / 2.0);
                var scale = Math.Min(width, height) / 24.0; // 基于24x24的基准尺寸

                switch (iconName)
                {
                    case "start_record":
                        // 播放三角形 - 绿色 (#27AE60)
                        var playBrush = new SolidColorBrush(WpfColor.FromRgb(39, 174, 96));
                        var playGeometry = new StreamGeometry();
                        using (var ctx = playGeometry.Open())
                        {
                            var size = 8 * scale;
                            ctx.BeginFigure(new WpfPoint(center.X - size, center.Y - size * 1.2), true, true);
                            ctx.LineTo(new WpfPoint(center.X - size, center.Y + size * 1.2), true, false);
                            ctx.LineTo(new WpfPoint(center.X + size, center.Y), true, false);
                        }
                        context.DrawGeometry(playBrush, null, playGeometry);
                        break;

                    case "pause":
                        // 暂停双竖线 - 橙色 (#F39C12)
                        var pauseBrush = new SolidColorBrush(WpfColor.FromRgb(243, 156, 18));
                        var barWidth = 3 * scale;
                        var barHeight = 12 * scale;
                        context.DrawRectangle(pauseBrush, null, new Rect(center.X - 6 * scale, center.Y - barHeight/2, barWidth, barHeight));
                        context.DrawRectangle(pauseBrush, null, new Rect(center.X + 3 * scale, center.Y - barHeight/2, barWidth, barHeight));
                        break;

                    case "stop":
                        // 停止按钮 - 现代红色设计，与stop_icon.svg一致
                        var stopOuterBrush = new SolidColorBrush(WpfColor.FromRgb(254, 226, 226)); // #FEE2E2
                        var stopBorderBrush = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // #EF4444
                        var stopInnerBrush = new SolidColorBrush(WpfColor.FromRgb(220, 38, 38)); // #DC2626
                        var stopHighlightBrush = new SolidColorBrush(WpfColor.FromRgb(252, 165, 165)); // #FCA5A5
                        
                        var stopRadius = 11 * scale;
                        var stopCenter = center;
                        
                        // 外圆背景
                        context.DrawEllipse(stopOuterBrush, 
                            new Pen(stopBorderBrush, 1.5 * scale), 
                            stopCenter, stopRadius, stopRadius);
                        
                        // 停止按钮 - 圆角方形
                        var stopButtonSize = 8 * scale;
                        var stopButtonRect = new Rect(
                            stopCenter.X - stopButtonSize/2, 
                            stopCenter.Y - stopButtonSize/2, 
                            stopButtonSize, stopButtonSize);
                        
                        // 绘制圆角矩形（使用多个矩形模拟圆角效果）
                        var cornerRadius = 1.5 * scale;
                        var innerSize = 7 * scale;
                        var innerRect = new Rect(
                            stopCenter.X - innerSize/2, 
                            stopCenter.Y - innerSize/2, 
                            innerSize, innerSize);
                        
                        // 主停止按钮
                        context.DrawRectangle(stopInnerBrush, null, stopButtonRect);
                        
                        // 内部高光效果
                        context.DrawRectangle(stopBorderBrush, null, innerRect);
                        
                        // 顶部高光
                        var topHighlightRect = new Rect(
                            stopCenter.X - 3 * scale, 
                            stopCenter.Y - innerSize/2, 
                            6 * scale, 1 * scale);
                        context.DrawRectangle(stopHighlightBrush, null, topHighlightRect);
                        
                        // 左侧高光
                        var leftHighlightRect = new Rect(
                            stopCenter.X - innerSize/2, 
                            stopCenter.Y - 3 * scale, 
                            1 * scale, 6 * scale);
                        context.DrawRectangle(stopHighlightBrush, null, leftHighlightRect);
                        break;

                    case "expand":
                        // 展开箭头 - 黑色 (#000000)
                        var expandPen = new Pen(new SolidColorBrush(Colors.Black), 2 * scale);
                        var arrowSize = 6 * scale;
                        // 左上角箭头
                        context.DrawLine(expandPen, 
                            new WpfPoint(center.X - arrowSize, center.Y - arrowSize), 
                            new WpfPoint(center.X - arrowSize, center.Y - arrowSize/2));
                        context.DrawLine(expandPen, 
                            new WpfPoint(center.X - arrowSize, center.Y - arrowSize), 
                            new WpfPoint(center.X - arrowSize/2, center.Y - arrowSize));
                        // 右下角箭头
                        context.DrawLine(expandPen, 
                            new WpfPoint(center.X + arrowSize, center.Y + arrowSize), 
                            new WpfPoint(center.X + arrowSize, center.Y + arrowSize/2));
                        context.DrawLine(expandPen, 
                            new WpfPoint(center.X + arrowSize, center.Y + arrowSize), 
                            new WpfPoint(center.X + arrowSize/2, center.Y + arrowSize));
                        break;

                    case "minimize":
                        // 最小化线条 - 黑色 (#000000)
                        var minimizePen = new Pen(new SolidColorBrush(Colors.Black), 2 * scale);
                        var lineLength = 8 * scale;
                        context.DrawLine(minimizePen, 
                            new WpfPoint(center.X - lineLength, center.Y), 
                            new WpfPoint(center.X + lineLength, center.Y));
                        break;

                    case "close":
                        // 关闭X - 黑色 (#000000)
                        var closePen = new Pen(new SolidColorBrush(Colors.Black), 2 * scale);
                        var crossSize = 6 * scale;
                        context.DrawLine(closePen, 
                            new WpfPoint(center.X - crossSize, center.Y - crossSize), 
                            new WpfPoint(center.X + crossSize, center.Y + crossSize));
                        context.DrawLine(closePen, 
                            new WpfPoint(center.X + crossSize, center.Y - crossSize), 
                            new WpfPoint(center.X - crossSize, center.Y + crossSize));
                        break;

                    default:
                        // 默认图标 - 简单圆形
                        var defaultBrush = new SolidColorBrush(Colors.Gray);
                        context.DrawEllipse(defaultBrush, null, center, 4 * scale, 4 * scale);
                        break;
                }
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            return bitmap;
        }

        /// <summary>
        /// 为Image控件设置图标
        /// </summary>
        public static void SetIconToImage(Image image, string iconName, int width, int height)
        {
            try
            {
                var bitmap = CreateIcon(iconName, width, height);
                image.Source = bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置图标失败 {iconName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量加载图标到窗口
        /// </summary>
        public static void LoadIconsToWindow(RecorderWindow window)
        {
            // 模态一图标 (24x24，按钮35x35，留出边距)
            SetIconToImage(window.RecordIcon1, "start_record", 24, 24);
            SetIconToImage(window.StopIcon1, "stop", 24, 24);
            SetIconToImage(window.ExpandIcon, "expand", 24, 24);
            
            // 模态二图标 (40x40 for main buttons, 20x20 for title bar)
            SetIconToImage(window.RecordIcon2, "start_record", 40, 40);
            SetIconToImage(window.StopIcon2, "stop", 40, 40);
            SetIconToImage(window.MinimizeIcon, "minimize", 20, 20);
            SetIconToImage(window.CloseIcon, "close", 20, 20);
        }

        /// <summary>
        /// 更新录音按钮的图标状态
        /// </summary>
        public static void UpdateRecordingIcon(Image image, bool isRecording, bool isPaused, int width, int height)
        {
            string iconName;
            
            if (isRecording && !isPaused)
            {
                iconName = "pause";
            }
            else
            {
                iconName = "start_record";
            }
            
            SetIconToImage(image, iconName, width, height);
        }
    }
}
