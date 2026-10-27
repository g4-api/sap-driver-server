using System;
using System.Collections.Generic;

namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Lists the simulator's input helps (F4 value lists) and their synthetic entries.
    /// </summary>
    /// <remarks>
    /// Each input help opens as a hit list popup with a grid view shell, as SAP GUI does when the hit list is shown as
    /// an ALV grid. The entries are fixed and synthetic, so tests can rely on their order and values.
    /// </remarks>
    public static class ValueHelpCatalog
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the airline input help.
        /// </summary>
        public const string Airline = "CARRID";

        /// <summary>
        /// Specifies the company code input help.
        /// </summary>
        public const string CompanyCode = "BUKRS";

        // Holds every input help, keyed by name.
        private static readonly Dictionary<string, ValueHelp> ValueHelps = new(StringComparer.Ordinal)
        {
            [Airline] = new ValueHelp
            {
                Columns = [Airline, "CARRNAME", "CURRCODE"],
                Headers = ["Airline", "Airline name", "Currency"],
                Rows =
                [
                    new ValueHelpRow("DMO", "Demo Jet", "USD"),
                    new ValueHelpRow("LAB", "Lab Lines", "EUR"),
                    new ValueHelpRow("QAX", "QA Express", "EUR"),
                    new ValueHelpRow("SIM", "Simulator Air", "EUR"),
                    new ValueHelpRow("TST", "Test Airways", "USD")
                ],
                Title = "Airline"
            },
            [CompanyCode] = new ValueHelp
            {
                Columns = [CompanyCode, "BUTXT", "ORT01"],
                Headers = ["Company code", "Company name", "City"],
                Rows =
                [
                    new ValueHelpRow("1000", "Simulator Inc.", "Sim City"),
                    new ValueHelpRow("2000", "Test Lab Ltd.", "Testville"),
                    new ValueHelpRow("3000", "Demo Corp.", "Demotown")
                ],
                Title = "Company code"
            }
        };
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets an input help by name.
        /// </summary>
        /// <param name="name">The input help name, one of the catalog constants.</param>
        /// <returns>The input help.</returns>
        /// <exception cref="KeyNotFoundException">The name is not a catalog input help.</exception>
        public static ValueHelp GetValueHelp(string name)
        {
            return ValueHelps[name];
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents one input help: its title, its three columns, and its entries.
        /// </summary>
        public sealed class ValueHelp
        {
            #region *** Properties   ***
            /// <summary>
            /// Gets the SAP column names, key column first, such as <c>CARRID</c>.
            /// </summary>
            public IReadOnlyList<string> Columns { get; init; }

            /// <summary>
            /// Gets the column headers, in column order.
            /// </summary>
            public IReadOnlyList<string> Headers { get; init; }

            /// <summary>
            /// Gets the entries in display order.
            /// </summary>
            public IReadOnlyList<ValueHelpRow> Rows { get; init; }

            /// <summary>
            /// Gets the title, which the hit list popup extends with the number of entries.
            /// </summary>
            public string Title { get; init; }
            #endregion
        }

        /// <summary>
        /// Represents one input help entry; the key is copied into the field.
        /// </summary>
        /// <remarks>
        /// The hit list grid binds its three columns to <see cref="Key"/>, <see cref="Text"/>, and <see cref="Extra"/>.
        /// </remarks>
        public sealed class ValueHelpRow
        {
            #region *** Constructors ***
            /// <summary>
            /// Initializes a new instance of the <see cref="ValueHelpRow"/> class.
            /// </summary>
            /// <param name="key">The value copied into the field, such as <c>SIM</c>.</param>
            /// <param name="text">The description, such as the airline name.</param>
            /// <param name="extra">An additional attribute, such as the currency.</param>
            public ValueHelpRow(string key, string text, string extra)
            {
                Key = key;
                Text = text;
                Extra = extra;
            }
            #endregion

            #region *** Properties   ***
            /// <summary>
            /// Gets an additional attribute, such as the currency.
            /// </summary>
            public string Extra { get; }

            /// <summary>
            /// Gets the value copied into the field, such as <c>SIM</c>.
            /// </summary>
            public string Key { get; }

            /// <summary>
            /// Gets the description, such as the airline name.
            /// </summary>
            public string Text { get; }
            #endregion
        }
        #endregion
    }
}
