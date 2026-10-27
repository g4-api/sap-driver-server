/*
 * RESOURCES
 * https://help.sap.com/docs/sap_gui_for_windows/b47d018c3b9b45e897faf66a6c0885a8/ce1d9e64355d49568e5def5271aea2db.html
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using Sap.DriverServer.Simulator.Scripting;
using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Raises SAP GUI's <c>GuiSession.Change</c> event for one session while it records (<c>Record = true</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribers are COM objects advised through the session's connection point. Each change calls their
    /// <c>IDispatch.Invoke</c> with <see cref="ChangeDispatchId"/> and the arguments session, component, and command
    /// array, where each command is an array of a kind (<c>SP</c> or <c>M</c>), a member name, and arguments.
    /// </para>
    /// <para>
    /// The simulator reports: button presses (<c>M press</c>), radio button selections (<c>M select</c>), check box
    /// changes (<c>SP selected</c>), field text (<c>SP text</c>), and virtual keys (<c>M sendVKey</c>). Like SAP GUI, it
    /// reports field text when the screen is submitted, before the action that submits it, not per keystroke. Tab
    /// selections, shell actions, and menu selections are not reported. The dispatch identifier and the command codes
    /// are the simulator's model of SAP GUI's event; they have not been compared with a real SAP GUI.
    /// </para>
    /// <para>
    /// Every member runs on the simulator's UI thread.
    /// </para>
    /// </remarks>
    public sealed class SessionRecorder
    {
        #region *** Constants    ***
        /// <summary>
        /// The dispatch identifier the simulator uses for <c>Change</c>.
        /// </summary>
        public const int ChangeDispatchId = 1;

        // Names the dispatch member that invokes an event by identifier, without a name lookup.
        private static readonly string ChangeMemberName = string.Create(CultureInfo.InvariantCulture, $"[DISPID={ChangeDispatchId}]");
        #endregion

        #region *** Fields       ***
        // Holds the fields whose text changed since the last submit, in change order, without repeats.
        private readonly List<FrameworkElement> _changedFields = [];

        // Owns the windows, the identifiers, and the system options.
        private readonly SimulatedSession _session;

        // Holds the advised subscribers by cookie.
        private readonly Dictionary<int, object> _subscribers = [];

        // Supplies the next subscription cookie; cookies start at 1 because 0 means no subscription.
        private int _nextCookie = 1;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="SessionRecorder"/> class.
        /// </summary>
        /// <param name="session">The session whose changes are reported.</param>
        public SessionRecorder(SimulatedSession session)
        {
            // Require the session; changes name it as their first argument.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the session records, which makes it raise <c>Change</c>.
        /// </summary>
        public bool IsRecording { get; private set; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Subscribes a COM object to the session's <c>Change</c> event.
        /// </summary>
        /// <param name="subscriber">The subscriber; it is called through <c>IDispatch</c>.</param>
        /// <returns>The cookie that identifies the subscription.</returns>
        public int Advise(object subscriber)
        {
            // Require the subscriber, then store it under a new cookie.
            ArgumentNullException.ThrowIfNull(argument: subscriber);

            var cookie = _nextCookie++;
            _subscribers[cookie] = subscriber;

            return cookie;
        }

        /// <summary>
        /// Starts reporting changes in a window: clicks and text changes inside it.
        /// </summary>
        /// <param name="window">The window, such as the main window or a popup.</param>
        public void Attach(FrameWindow window)
        {
            // Listen to bubbling events, including ones a control already handled, so every click and edit is seen.
            ArgumentNullException.ThrowIfNull(argument: window);

            window.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnClick), handledEventsToo: true);
            window.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged), handledEventsToo: true);
            window.AddHandler(PasswordBox.PasswordChangedEvent, new RoutedEventHandler(OnTextChanged), handledEventsToo: true);
        }

        /// <summary>
        /// Reports a virtual key sent to a window, after the field text it submits.
        /// </summary>
        /// <param name="window">The window that receives the key.</param>
        /// <param name="key">The SAP virtual key.</param>
        public void RecordVirtualKey(FrameWindow window, int key)
        {
            // Report only while recording; the window is the component, as in SAP GUI.
            if (!IsRecording)
            {
                return;
            }

            ReportChangedFields();
            RaiseChange(GuiComponentFactory.NewWindow(window), NewCommand("M", "sendVKey", key));
        }

        /// <summary>
        /// Switches recording on or off.
        /// </summary>
        /// <param name="value">True to record.</param>
        /// <exception cref="SimulatorException">Recording is disabled, as by <c>sapgui/user_scripting_disable_recording</c>.</exception>
        public void SetRecording(bool value)
        {
            // Refuse recording when the scenario disables it, as SAP GUI does for such systems.
            if (value && _session.System.Options.IsRecordingDisabled)
            {
                throw new SimulatorException("Recording is disabled for this system.");
            }

            // Start from a clean slate either way, so text changed before recording is never reported.
            IsRecording = value;
            _changedFields.Clear();
        }

        /// <summary>
        /// Ends a subscription; unknown cookies are ignored.
        /// </summary>
        /// <param name="cookie">The cookie returned by <see cref="Advise"/>.</param>
        public void Unadvise(int cookie)
        {
            _subscribers.Remove(cookie);
        }

        // Builds one command: kind, member name, then arguments.
        private static object[] NewCommand(string kind, string name, params object[] arguments)
        {
            return [kind, name, .. arguments];
        }

        // Reports a click: buttons press, radio buttons select, and check boxes report their new state.
        private void OnClick(object sender, RoutedEventArgs e)
        {
            // Ignore clicks while not recording and clicks outside scripting controls.
            var element = IsRecording ? ScriptingTree.FindElement(e.OriginalSource as DependencyObject) : null;
            if (element is null)
            {
                return;
            }

            // Name the action by control type; other clickable parts, such as tabs, are not reported.
            var component = GuiComponentFactory.NewComponent(element);
            object[] command = component switch
            {
                GuiButton => NewCommand("M", "press"),
                GuiRadioButton => NewCommand("M", "select"),
                GuiCheckBox checkBox => NewCommand("SP", "selected", checkBox.Selected),
                _ => null
            };

            if (command is null)
            {
                return;
            }

            // Report the submitted field text first, as SAP GUI does, then the click.
            ReportChangedFields();
            RaiseChange(component, command);
        }

        // Marks an editable field as changed; screens filling their own fields while they load are not changes.
        private void OnTextChanged(object sender, RoutedEventArgs e)
        {
            // Ignore changes while not recording, outside scripting controls, and in controls that are not loaded yet.
            var element = IsRecording ? ScriptingTree.FindElement(e.OriginalSource as DependencyObject) : null;
            if (element is null || !element.IsLoaded || element is TextBoxBase { IsReadOnly: true })
            {
                return;
            }

            // Keep each field once, in the order it first changed.
            if (!_changedFields.Contains(element))
            {
                _changedFields.Add(element);
            }
        }

        // Calls every subscriber's Change; subscribers that fail are dropped, as SAP GUI drops disconnected clients.
        private void RaiseChange(GuiComponent component, object[] command)
        {
            // Pass the session, the component, and a command array of one command.
            var arguments = new object[] { new GuiSession(_session), component, new object[] { command } };

            foreach (var (cookie, subscriber) in _subscribers.ToList())
            {
                try
                {
                    subscriber.GetType().InvokeMember(
                        ChangeMemberName,
                        BindingFlags.InvokeMethod,
                        binder: null,
                        subscriber,
                        arguments,
                        CultureInfo.InvariantCulture);
                }
                catch (Exception exception) when (exception is COMException or TargetInvocationException or InvalidComObjectException)
                {
                    _subscribers.Remove(cookie);
                }
            }
        }

        // Reports the text of every field changed since the last submit, then forgets them.
        private void ReportChangedFields()
        {
            // Read each field through its scripting object, so the reported text matches what scripting reads.
            var fields = _changedFields.ToList();
            _changedFields.Clear();

            foreach (var component in fields.Where(i => i.IsLoaded).Select(GuiComponentFactory.NewComponent))
            {
                RaiseChange(component, NewCommand("SP", "text", component.Text));
            }
        }
        #endregion
    }
}
