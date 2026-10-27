using System;
using System.Globalization;
using System.Windows.Data;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Computes the left edge of a screen canvas tag, so the tag starts at its frame and never leaves the screenshot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The values are, in order: the highlight's left edge, its frame thickness, the screenshot width (all in image
    /// pixels), the tag's own width before scaling, and the tag scale. The tag normally starts at the frame's outer left
    /// edge; near the right edge it shifts left so its end stays inside the screenshot, and it never starts before 0.
    /// </para>
    /// <para>
    /// The tag width is only known after layout, so the view binds it from the tag itself and the converter runs again
    /// once it is measured. Missing or unexpected values place the tag at 0.
    /// </para>
    /// </remarks>
    public sealed class CanvasTagLeftConverter : IMultiValueConverter
    {
        #region *** Methods      ***
        /// <inheritdoc />
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Without all five numbers there is nothing to align against; start at the left edge.
            if (values is not { Length: 5 } || !TryGetNumbers(values, out var numbers))
            {
                return 0d;
            }

            // Start at the frame's outer edge, then pull back so the scaled tag ends inside the screenshot.
            var (left, frame, imageWidth, tagWidth, tagScale) = numbers;
            var start = left - frame;
            var latest = imageWidth - (tagWidth * tagScale);

            return Math.Max(0, Math.Min(start, latest));
        }

        /// <inheritdoc />
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // The tag position is computed for display only and is never written back.
            throw new NotSupportedException();
        }

        // Reads the five bound values as numbers; the screenshot width arrives as an integer.
        private static bool TryGetNumbers(object[] values, out (double Left, double Frame, double ImageWidth, double TagWidth, double TagScale) numbers)
        {
            // Convert each value; a value that is not numeric, such as UnsetValue during layout, fails the whole set.
            var converted = new double[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                if (values[index] is not (double or int))
                {
                    numbers = default;
                    return false;
                }

                converted[index] = System.Convert.ToDouble(values[index], CultureInfo.InvariantCulture);
            }

            numbers = (converted[0], converted[1], converted[2], converted[3], converted[4]);
            return true;
        }
        #endregion
    }
}
