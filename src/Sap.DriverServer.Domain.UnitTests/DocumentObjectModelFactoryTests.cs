using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("DocumentObjectModelFactory")]
    [TestCategory("UnitTest")]
    public class DocumentObjectModelFactoryTests
    {
        [TestMethod(DisplayName = "Verify that the document mirrors the scripting tree with SAP types as element names and identity attributes.")]
        public void NewDocumentStructureTest()
        {
            // Arrange: a session with one button.
            var application = FakeSapTree.NewApplication();
            var session = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]");
            var userArea = (FakeSapGuiNode)session.FindById("wnd[0]/usr");
            FakeSapTree.AddControl(userArea, name: "btnOK", type: "GuiButton").With("Text", "OK");

            // Act: build the session document.
            var document = DocumentObjectModelFactory.NewDocument(session);

            // Assert: the root is the session and the button carries its identity and display attributes.
            Assert.AreEqual("GuiSession", document.Root.Name.LocalName);
            var button = document.Descendants("GuiButton").Single();
            Assert.AreEqual("/app/con[0]/ses[0]/wnd[0]/usr/btnOK", (string)button.Attribute("id"));
            Assert.AreEqual("GuiButton", (string)button.Attribute("type"));
            Assert.AreEqual("btnOK", (string)button.Attribute("name"));
            Assert.AreEqual("OK", (string)button.Attribute("text"));
            Assert.AreEqual("true", (string)button.Attribute("changeable"));
        }

        [TestMethod(DisplayName = "Verify that password text is never read into the document and session-level secrets are absent.")]
        public void NewDocumentOmitsSecretsTest()
        {
            // Arrange: a session with a filled password field; the session info holds a user name.
            var application = FakeSapTree.NewApplication();
            var session = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]");
            var userArea = (FakeSapGuiNode)session.FindById("wnd[0]/usr");
            var password = FakeSapTree.AddControl(userArea, name: "pwdBCODE", type: "GuiPasswordField").With("Text", "TopSecret1");

            // Act: build the session document.
            var xml = DocumentObjectModelFactory.NewDocument(session).ToString();

            // Assert: neither the password nor the logged-on user appears, and the password text was never requested.
            Assert.DoesNotContain("TopSecret1", xml);
            Assert.DoesNotContain("SECRET-USER", xml);
            Assert.DoesNotContain("Text", password.PropertyReads);
        }

        [TestMethod(DisplayName = "Verify that shells carry their subType and absent properties are omitted rather than failing.")]
        public void NewDocumentShellAndAbsentPropertiesTest()
        {
            // Arrange: a grid shell and a control that exposes only its identity.
            var application = FakeSapTree.NewApplication();
            var session = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]");
            var userArea = (FakeSapGuiNode)session.FindById("wnd[0]/usr");
            var grid = FakeSapTree.AddControl(userArea, name: "shellcont/shell", type: "GuiShell");
            grid.Properties["SubType"] = "GridView";
            userArea.Add(new FakeSapGuiNode($"{userArea.Id}/lblBARE", "GuiLabel") { IsContainer = false });

            // Act: build the session document.
            var document = DocumentObjectModelFactory.NewDocument(session);

            // Assert: the shell subtype is present and the bare label has identity attributes only.
            Assert.AreEqual("GridView", (string)document.Descendants("GuiShell").Single().Attribute("subType"));
            var label = document.Descendants("GuiLabel").Single();
            Assert.IsNull(label.Attribute("text"));
            Assert.IsNotNull(label.Attribute("id"));
        }
    }
}
