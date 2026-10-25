using Sap.DriverServer.Domain;

namespace Sap.DriverServer.Domain.UnitTests.Support
{
    /// <summary>
    /// Builds realistic in-memory SAP GUI Scripting trees: application, connections, sessions, windows, and controls.
    /// </summary>
    public static class FakeSapTree
    {
        #region *** Methods      ***
        /// <summary>
        /// Adds a connection to an application node.
        /// </summary>
        /// <param name="application">The application node; the new connection gets the next <c>con[n]</c> index.</param>
        /// <param name="description">The SAP Logon description.</param>
        /// <param name="isDisabledByServer">True when the server disabled scripting for the connection.</param>
        /// <returns>The connection node.</returns>
        public static FakeSapGuiNode AddConnection(FakeSapGuiNode application, string description, bool isDisabledByServer)
        {
            // Model the GuiConnection properties read during discovery, numbering connections in creation order.
            var index = application.Children.Count;
            var connection = application.Add(new FakeSapGuiNode($"/app/con[{index}]", "GuiConnection"));
            connection.Properties["Description"] = description;
            connection.Properties["DisabledByServer"] = isDisabledByServer;
            connection.Properties["Name"] = $"con[{index}]";

            return connection;
        }

        /// <summary>
        /// Adds a control to a container node.
        /// </summary>
        /// <param name="container">The container node.</param>
        /// <param name="name">The relative identifier segment, such as <c>btnOK</c> or <c>txtRSYST-BNAME</c>.</param>
        /// <param name="type">The SAP type name.</param>
        /// <returns>The control node with empty text; leaves expose no Children collection.</returns>
        public static FakeSapGuiNode AddControl(FakeSapGuiNode container, string name, string type)
        {
            // Model the common GuiVComponent properties used by the DOM and interactions.
            var control = container.Add(new FakeSapGuiNode($"{container.Id}/{name}", type));
            control.IsContainer = false;
            control.Properties["Changeable"] = true;
            control.Properties["Name"] = name;
            control.Properties["Text"] = string.Empty;
            control.Properties["Tooltip"] = string.Empty;

            return control;
        }

        /// <summary>
        /// Adds a session with a main window (<c>wnd[0]</c>), user area, and status bar to a connection.
        /// </summary>
        /// <param name="connection">The connection node; the new session gets the next <c>ses[n]</c> index.</param>
        /// <param name="systemName">The SAP system identifier.</param>
        /// <param name="client">The SAP client.</param>
        /// <returns>The session node.</returns>
        public static FakeSapGuiNode AddSession(FakeSapGuiNode connection, string systemName, string client)
        {
            // Model GuiSession with GuiSessionInfo, which provides the system and client used for selection.
            var index = connection.Children.Count;
            var session = connection.Add(new FakeSapGuiNode($"{connection.Id}/ses[{index}]", SapGuiTypes.Session));
            var info = new FakeSapGuiNode($"{session.Id}/info", "GuiSessionInfo")
            {
                IsContainer = false
            };
            info.Properties["Client"] = client;
            info.Properties["SystemName"] = systemName;
            info.Properties["User"] = "SECRET-USER";
            session.Properties["Busy"] = false;
            session.Properties["Info"] = info;
            session.Properties["Name"] = $"ses[{index}]";

            // Add the main window with a user area and a status bar, and make it the active window.
            var window = AddWindow(session, title: "SAP Easy Access", handle: 0x1A2B + index);
            var userArea = window.Add(new FakeSapGuiNode($"{window.Id}/usr", "GuiUserArea"));
            userArea.Properties["Name"] = "usr";
            var statusBar = AddControl(window, name: "sbar", type: "GuiStatusbar").With("Text", "Saved");
            statusBar.Properties["MessageId"] = "00";
            statusBar.Properties["MessageNumber"] = "344";
            statusBar.Properties["MessageType"] = "S";
            session.Properties["ActiveWindow"] = window;

            return session;
        }

        /// <summary>
        /// Adds a frame window to a session.
        /// </summary>
        /// <param name="session">The session node; the new window gets the next <c>wnd[n]</c> index.</param>
        /// <param name="title">The window title.</param>
        /// <param name="handle">The numeric window handle.</param>
        /// <returns>The window node; <c>wnd[0]</c> is the main window and later windows are modal.</returns>
        public static FakeSapGuiNode AddWindow(FakeSapGuiNode session, string title, int handle)
        {
            // Model GuiFrameWindow geometry and identity, numbering windows in creation order.
            var index = session.Children.Count;
            var window = session.Add(new FakeSapGuiNode($"{session.Id}/wnd[{index}]", index == 0 ? "GuiMainWindow" : "GuiModalWindow"));
            window.Properties["Handle"] = handle;
            window.Properties["Height"] = 600;
            window.Properties["Name"] = $"wnd[{index}]";
            window.Properties["ScreenLeft"] = 10;
            window.Properties["ScreenTop"] = 20;
            window.Properties["Text"] = title;
            window.Properties["Width"] = 800;

            return window;
        }

        /// <summary>
        /// Creates an application with one connection (<c>DEV</c>) and one session (system DEV, client 100).
        /// </summary>
        /// <returns>The application node.</returns>
        public static FakeSapGuiNode NewApplication()
        {
            // Build the most common shape: one SAP Logon connection with one session.
            var application = NewEmptyApplication();
            var connection = AddConnection(application, description: "DEV - Development", isDisabledByServer: false);
            AddSession(connection, systemName: "DEV", client: "100");

            return application;
        }

        /// <summary>
        /// Creates an application node without connections.
        /// </summary>
        /// <returns>The application node.</returns>
        public static FakeSapGuiNode NewEmptyApplication()
        {
            // Model GuiApplication, whose Children are the open connections.
            return new FakeSapGuiNode("/app", "GuiApplication");
        }
        #endregion
    }
}
