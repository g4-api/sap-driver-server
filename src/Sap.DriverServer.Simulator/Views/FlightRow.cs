using System.Collections.Generic;
using System.Globalization;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Represents one synthetic flight in the grid screen.
    /// </summary>
    /// <remarks>
    /// The rows are generated, not taken from any system, so the simulator holds no business data. Values are strings,
    /// as SAP's <c>GetCellValue</c> returns them.
    /// </remarks>
    public sealed class FlightRow
    {
        #region *** Constants    ***
        // Lists the synthetic airline codes; they do not belong to real airlines.
        private static readonly string[] Airlines = ["SIM", "TST", "DMO"];

        // Holds the number of generated rows.
        private const int RowCount = 12;
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the airline code (column <c>CARRID</c>).
        /// </summary>
        public string Airline { get; init; }

        /// <summary>
        /// Gets the currency (column <c>CURRENCY</c>).
        /// </summary>
        public string Currency { get; init; }

        /// <summary>
        /// Gets the flight date (column <c>FLDATE</c>).
        /// </summary>
        public string FlightDate { get; init; }

        /// <summary>
        /// Gets the flight number (column <c>CONNID</c>).
        /// </summary>
        public string FlightNumber { get; init; }

        /// <summary>
        /// Gets the price (column <c>PRICE</c>).
        /// </summary>
        public string Price { get; init; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets a cell value by SAP column name.
        /// </summary>
        /// <param name="column">The column name, such as <c>PRICE</c>.</param>
        /// <returns>The value; null when the grid has no such column.</returns>
        public string GetValue(string column)
        {
            // Map each SAP column name to its property.
            return column switch
            {
                "CARRID" => Airline,
                "CONNID" => FlightNumber,
                "CURRENCY" => Currency,
                "FLDATE" => FlightDate,
                "PRICE" => Price,
                _ => null
            };
        }

        /// <summary>
        /// Creates the grid's rows: the same values on every run, so tests can assert them.
        /// </summary>
        /// <returns>The rows in display order.</returns>
        public static IList<FlightRow> NewRows()
        {
            // Derive every value from the row index; nothing is random.
            var rows = new List<FlightRow>(RowCount);

            for (var index = 0; index < RowCount; index++)
            {
                rows.Add(new FlightRow
                {
                    Airline = Airlines[index % Airlines.Length],
                    Currency = index % 2 == 0 ? "EUR" : "USD",
                    FlightDate = string.Create(CultureInfo.InvariantCulture, $"2026-11-{index + 1:00}"),
                    FlightNumber = string.Create(CultureInfo.InvariantCulture, $"{17 + (index * 4):0000}"),
                    Price = string.Create(CultureInfo.InvariantCulture, $"{120.50m + (index * 37.25m):0.00}")
                });
            }

            return rows;
        }
        #endregion
    }
}
