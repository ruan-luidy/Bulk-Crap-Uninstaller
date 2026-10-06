/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BulkCrapUninstaller.Wpf.Shared.Controls
{
    // As many columns as fit; each child goes to the shortest column
    public class MasonryPanel : Panel
    {
        public static readonly DependencyProperty MinColumnWidthProperty =
            DependencyProperty.Register(nameof(MinColumnWidth), typeof(double), typeof(MasonryPanel),
                new FrameworkPropertyMetadata(320.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(MasonryPanel),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MaxColumnsProperty =
            DependencyProperty.Register(nameof(MaxColumns), typeof(int), typeof(MasonryPanel),
                new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty LevelColumnsProperty =
            DependencyProperty.Register(nameof(LevelColumns), typeof(bool), typeof(MasonryPanel),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange));

        public double MinColumnWidth
        {
            get => (double)GetValue(MinColumnWidthProperty);
            set => SetValue(MinColumnWidthProperty, value);
        }

        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        public int MaxColumns
        {
            get => (int)GetValue(MaxColumnsProperty);
            set => SetValue(MaxColumnsProperty, value);
        }

        // Stretch the last card of each column down to the bottom of the tallest one
        public bool LevelColumns
        {
            get => (bool)GetValue(LevelColumnsProperty);
            set => SetValue(LevelColumnsProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? MinColumnWidth : availableSize.Width;
            var (columns, columnWidth) = Columns(width);
            var heights = new double[columns];

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(columnWidth, double.PositiveInfinity));
                if (child.Visibility == Visibility.Collapsed)
                    continue;

                heights[Shortest(heights)] += child.DesiredSize.Height + Spacing;
            }

            return new Size(width, Math.Max(0, heights.Max() - Spacing));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var (columns, columnWidth) = Columns(finalSize.Width);
            var heights = new double[columns];
            var placed = new List<(UIElement Child, int Column, double Top)>();

            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    child.Arrange(new Rect());
                    continue;
                }

                var column = Shortest(heights);
                placed.Add((child, column, heights[column]));
                heights[column] += child.DesiredSize.Height + Spacing;
            }

            var bottom = Math.Max(0, heights.Max() - Spacing);
            for (var i = 0; i < placed.Count; i++)
            {
                var (child, column, top) = placed[i];
                var height = child.DesiredSize.Height;
                if (LevelColumns && !placed.Skip(i + 1).Any(x => x.Column == column))
                    height = bottom - top;

                child.Arrange(new Rect(column * (columnWidth + Spacing), top, columnWidth, height));
            }

            return finalSize;
        }

        private (int Columns, double Width) Columns(double width)
        {
            var columns = (int)Math.Floor((width + Spacing) / (MinColumnWidth + Spacing));
            columns = Math.Clamp(columns, 1, Math.Max(1, MaxColumns));
            return (columns, Math.Max(0, (width - Spacing * (columns - 1)) / columns));
        }

        private static int Shortest(double[] heights)
        {
            var index = 0;
            for (var i = 1; i < heights.Length; i++)
            {
                if (heights[i] < heights[index] - 0.5)
                    index = i;
            }

            return index;
        }
    }
}
