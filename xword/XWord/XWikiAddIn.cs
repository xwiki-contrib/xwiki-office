﻿#region LGPL license

/*
 * See the NOTICE file distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This is free software; you can redistribute it and/or modify it
 * under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 2.1 of
 * the License, or (at your option) any later version.
 *
 * This software is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this software; if not, write to the Free
 * Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
 * 02110-1301 USA, or see the FSF site: http://www.fsf.org.
 */

#endregion //license

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Timers;
using System.Xml.Linq;
using System.Configuration;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Word = Microsoft.Office.Interop.Word;
using Office = Microsoft.Office.Core;
using Tools = Microsoft.Office.Tools;
using Security = System.Security;
using XWord.VstoExtensions;
using XOffice;
using XWiki.Clients;
using XWiki;
using XWiki.Model;
using XWiki.Logging;
using XWiki.Prefetching;
using UICommons;

namespace XWord
{
    public partial class XWikiAddIn
    {
        #region declarations

        System.Timers.Timer timer;
        const int TIMER_INTERVAL = 2000;
        bool bShowTaskPanes = true;
        /// <summary>
        /// Specifies the path to the active document.
        /// </summary>
        public string currentLocalFilePath = "";
        /// <summary>
        /// Specifies the full name of the currently edited page(if any).
        /// </summary>
        public string currentPageFullName = "";

        /// <summary>
        /// A list of the web client's cookies.
        /// </summary>
        public static List<String> cookies = new List<string>();
        /// <summary>
        /// The url of the server that the user is currently connected to.
        /// </summary>
        public string serverURL = "";
        /// <summary>
        /// The username used to connect to the server.
        /// </summary>
        public string username = "";
        /// <summary>
        /// The password used to connect to the server.
        /// </summary>
        public string password = "";
        private Word.WdSaveFormat saveFormat;
        /// <summary>
        /// Object containing the structure(Spaces,Pages,Attachment names)
        /// of the wiki the use is connected to.
        /// </summary>
        public Wiki wiki = null;
        private IXWikiClient client;
        private AddinActions addinActions;
        private XWikiNavigationPane xWikiTaskPane;
        private AddinStatus addinStatus;
        private Word.Document lastActiveDocument;
        private Word.Range activeDocumentContent;
        private String lastActiveDocumentFullName;
        private bool rememberCredentials = false;
        /// <summary>
        /// Collection containing all custom task panes in all opened Word instances.
        /// </summary>
        public Dictionary<String, XWikiNavigationPane> panes = new Dictionary<string, XWikiNavigationPane>();
        /// <summary>
        /// A list with the pages that cannot be edited with Word.
        /// </summary>
        private List<String> protectedPages = new List<string>();

        private IAnnotationMaintainer annotationMainteiner;
        /// <summary>
        /// A dictionary that contains a key value pair with the local file name of the document
        /// and the full name of the associated wiki page.
        /// </summary>
        private Dictionary<String, String> editedPages = new Dictionary<string, string>();
        private XOfficeCommonSettings addinSettings;
        
        #endregion

        ///Delegate and event declarations
        #region Delegates&Events

        // A delegate type for hooking up Client instance change notifications.
        public delegate void ClientInstanceChangedHandler(object sender, EventArgs e);
        //Event triggered when the client instance is changed.
        public event ClientInstanceChangedHandler ClientInstanceChanged;

        /// <summary>
        /// Delegate for handling successful logins.
        /// </summary>
        public delegate void LoginSuccessfulHandler();
        /// <summary>
        /// Envent triggered when a successful login is made.
        /// </summary>
        public event LoginSuccessfulHandler LoginSuccessul;

        /// <summary>
        /// Delegate for handling failed logins
        /// </summary>
        public delegate void LoginFailedHandler();
        /// <summary>
        /// Event triggered when a failed login is made.
        /// </summary>
        public event LoginFailedHandler LoginFailed;

        /// <summary>
        /// Delegate for handling active document swithing.
        /// </summary>
        public delegate void DocumentChangedHandler();

        /// <summary>
        /// Event triggered when the active document is changed.
        /// </summary>
        public event DocumentChangedHandler DocumentChanged;


        #endregion//Delegates&Events


        /// <summary>
        /// Gets or sets the wildcards for the protected pages.
        /// The protected pages contain scripts and cannot be editited with Word.
        /// </summary>
        public List<String> ProtectedPages
        {
            get { return protectedPages; }
            set { protectedPages = value; }
        }

        /// <summary>
        /// Common settings for the addins.
        /// </summary>
        public XOfficeCommonSettings AddinSettings
        {
            get { return addinSettings; }
            set { addinSettings = value; }
        }

        /// <summary>
        /// Specifies if the user has stored credentials.
        /// </summary>
        public bool RememberCredentials
        {
            get { return rememberCredentials; }
            set { rememberCredentials = value; }
        }

        /// <summary>
        /// Gets a dictionary with the edited pages list.
        /// </summary>
        public Dictionary<String, String> EditedPages
        {
            get { return editedPages; }
        }


        /// <summary>
        /// Specifies if the current page was published on the server.
        /// It does not specify if the last modifications were saved, but
        /// if the local document has a coresponding wiki page. It's FALSE
        /// until first saving to wiki.
        /// </summary>
        public bool CurrentPagePublished
        {
            get
            {
                XWikiDocument currentDoc = wiki.GetPageById(currentPageFullName);
                return currentDoc.published;
            }
            set
            {
                XWikiDocument currentDoc = wiki.GetPageById(currentPageFullName);
                currentDoc.published = value;
            }
        }

        /// <summary>
        /// Contains the states of the addin.
        /// </summary>
        public AddinStatus AddinStatus
        {
            get 
            {
                if (addinStatus == null)
                {
                    addinStatus = new AddinStatus();
                }
                return addinStatus;
            }
            set { addinStatus = value; }
        }

        /// <summary>
        /// Gets the last known instance for the active document.
        /// </summary>
        public Word.Document ActiveDocumentInstance
        {
            get
            {
                try
                {
                    if (this.Application.ActiveDocument != null && this.Application.ActiveDocument.FullName != null)
                    {
                        lastActiveDocument = this.Application.ActiveDocument;
                        lastActiveDocumentFullName = this.Application.ActiveDocument.FullName;
                        return this.Application.ActiveDocument;
                    }
                    else
                    {
                        //lastActiveDocument.Activate();
                        return lastActiveDocument;
                    }
                }
                catch (COMException)
                {
                    return lastActiveDocument;
                }
            }
        }

        /// <summary>
        /// Gets the full name of the active document.
        /// <remarks>
        /// It is recomended to use this property instead of VSTO's Application.ActiveDocument.FullName
        /// </remarks>
        /// </summary>
        public String ActiveDocumentFullName
        {
            get
            {
                try
                {
                    if (this.Application.ActiveDocument.FullName != null)
                    {
                        lastActiveDocumentFullName = Application.ActiveDocument.FullName;
                    }
                    return lastActiveDocumentFullName;
                }
                //Word deletes the FullName object;
                catch (COMException)
                {
                    return lastActiveDocumentFullName;
                }
            }
        }

        /// <summary>
        /// Gets a range of the active document's content.
        /// </summary>
        public Word.Range ActiveDocumentContentRange
        {
            get
            {
                try
                {
                    if (this.Application.ActiveDocument.Content != null)
                    {
                        activeDocumentContent = this.Application.ActiveDocument.Content;
                    }
                    return activeDocumentContent;
                }
                catch (COMException)
                {
                    return activeDocumentContent;
                }
            }
        }

        /// <summary>
        /// The Wiki Explorer of the active window.
        /// </summary>
        public XWikiNavigationPane XWikiTaskPane
        {
            get { return xWikiTaskPane; }
            set { xWikiTaskPane = value; }
        }

        /// <summary>
        /// A collection containing all custom taskpanes provided by Word extensions.
        /// <remarks>Non XWiki extensions included.</remarks>
        /// </summary>
        public Tools.CustomTaskPaneCollection XWikiCustomTaskPanes
        {
            get { return this.CustomTaskPanes; }
        }

        /// <summary>
        /// Provides functionality for common XWiki actions, like creating and editing pages, adding attachments and others.
        /// </summary>
        public AddinActions AddinActions
        {
            get { return addinActions; }
            set { addinActions = value; }
        }

        /// <summary>
        /// The Custom WebClient that communicates with the server.
        /// </summary>
        public IXWikiClient Client
        {
            get 
            {
                return client;
            }
            set
            {
                client = value;
                ClientInstanceChanged(this, null);
            }
        }

        /// <summary>
        /// An AnnotationMaintainer instance for annotaions updates.
        /// </summary>
        public IAnnotationMaintainer AnnotationMaintainer
        {
            get
            {
                return this.annotationMainteiner; ;
            }
            set
            {
                this.annotationMainteiner = value;
            }
        }

        /// <summary>
        /// Gets or sets the save format for the html documents.
        /// </summary>
        public Word.WdSaveFormat SaveFormat
        {
            get { return saveFormat; }
            set { saveFormat = value; }
        }

        /// <summary>
        /// Gets the value of the default syntax.
        /// </summary>
        public String DefaultSyntax
        {
            get 
            {
                return Client.GetDefaultServerSyntax(); 
            }
        }

        /// <summary>
        /// Event triggered when a new word instance is starting.
        /// </summary>
        /// <param name="sender">The control/application that triggers the event.</param>
        /// <param name="e">The event parameters.</param>
        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            try
            {
                InitializeAddin();
                this.Application.DocumentBeforeClose += new Microsoft.Office.Interop.Word.ApplicationEvents4_DocumentBeforeCloseEventHandler(Application_DocumentBeforeClose);
                this.Application.DocumentChange += new Microsoft.Office.Interop.Word.ApplicationEvents4_DocumentChangeEventHandler(Application_DocumentChange);
                this.Application.DocumentOpen += new Microsoft.Office.Interop.Word.ApplicationEvents4_DocumentOpenEventHandler(Application_DocumentOpen);
                ((Word.ApplicationEvents4_Event)this.Application).NewDocument += new Microsoft.Office.Interop.Word.ApplicationEvents4_NewDocumentEventHandler(ThisAddIn_NewDocument);
            }
            catch (Exception ex)
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Break();
                }
                else
                {
                    UserNotifier.Error(ex.Message);
                }
            }
        }

        /// <summary>
        /// Event triggered when a new document is created.
        /// </summary>
        /// <param name="Doc">The instance of the document.</param>
        void ThisAddIn_NewDocument(Microsoft.Office.Interop.Word.Document Doc)
        {
            AddTaskPane(Doc);
        }

        /// <summary>
        /// Event triggered when a document is openend.
        /// </summary>
        /// <param name="Doc">The instance of the document.</param>
        void Application_DocumentOpen(Microsoft.Office.Interop.Word.Document Doc)
        {
            AddTaskPane(Doc);
        }

        /// <summary>
        /// Event triggered when the active document changes.
        /// <remarks>This event is triggered every time you switch to a different document window.
        /// It does NOT refer to content changing.</remarks>
        /// </summary>
        void Application_DocumentChange()
        {
            //Remove the orphan task panes.
            RemoveOrphans();
            //Reassign values to the document and wiki page states. 
            lastActiveDocument = ActiveDocumentInstance;
            lastActiveDocumentFullName = ActiveDocumentFullName;
            activeDocumentContent = ActiveDocumentContentRange;
            //if current document is a wiki page.
            if (EditedPages.ContainsKey(lastActiveDocumentFullName))
            {
                currentPageFullName = EditedPages[lastActiveDocumentFullName];
            }
            else
            {
                currentPageFullName = null;
            }
            //Update the toggle button.
            UpdateWikiExplorerButtonState();

            //Trigger event
            DocumentChanged();
        }

        /// <summary>
        /// Event triggered before closing a document.
        /// </summary>
        /// <param name="doc">The instance of the document.</param>
        /// <param name="cancel">Reference to a variable stating if the operation should be canceled.
        /// Switch the value to 'true' to cancle the closing.
        /// </param>
        void Application_DocumentBeforeClose(Microsoft.Office.Interop.Word.Document doc, ref bool cancel)
        {
            string docFullName = doc.FullName;
            //if is edited wiki page
            if (EditedPages.ContainsKey(docFullName))
            {
                //Prevent default save dialog from appearing.
                doc.Saved = true;
                //TODO: display a custom dialog for saving to the wiki.
            }
            RemoveTaskPane(doc);
            if(EditedPages.ContainsKey(doc.FullName))
            {
                EditedPages.Remove(doc.FullName);
            }
        }

        /// <summary>
        /// Event triggered before closing the addin.
        /// </summary>
        /// <param name="sender">The instance of the sender.</param>
        /// <param name="e">The event parameters.</param>
        private void ThisAddIn_Shutdown(object sender, System.EventArgs e)
        {
            RemoveAllTaskPanes();
            Log.Information("XWord closed");
        }

        /// <summary>
        /// Adds a custom task pane to the addin's taskpanes collection.
        /// The task pane is assigned to the given document's active window.
        /// </summary>
        /// <param name="doc">The instance of the document.</param>
        private void AddTaskPane(Word.Document doc)
        {
            XWikiNavigationPane paneControl = new XWikiNavigationPane(this);
            if (GetWikiExplorer(doc) == null)
            {
                //attach a new Wiki Explorer to the document window.
                Tools.CustomTaskPane ctp = this.CustomTaskPanes.Add(paneControl, XWikiNavigationPane.TASK_PANE_TITLE,
                                                                doc.ActiveWindow);
                this.XWikiTaskPane = paneControl;
                ctp.Visible = true;
                ctp.VisibleChanged += new EventHandler(wikiExplorer_VisibleChanged);
            }            
        }
        
        private void wikiExplorer_VisibleChanged(object sender, EventArgs e)
        {
            UpdateWikiExplorerButtonState();
        }

        private void UpdateWikiExplorerButtonState()
        {
            XWikiNavigationPane activePane = XWikiTaskPane;
            Tools.CustomTaskPane ctp = GetActiveWikiExplorer();
            Globals.Ribbons.XWikiRibbon.SetWikiExplorerButtonState(ctp.Visible);
        }

        private Tools.CustomTaskPane GetActiveWikiExplorer()
        {
            return GetWikiExplorer(Application.ActiveWindow);
        }

        private Tools.CustomTaskPane GetWikiExplorer(Word.Document document)
        {
            //required by COM interop
            object first = 1;
            return GetWikiExplorer(document.Windows.get_Item(ref first));
        }

        /// <summary>
        /// Gets the attached Wiki Explorer of a Word Window.
        /// </summary>
        /// <param name="window">The Word window contining the Wiki Explorer.</param>
        /// <returns>The instance of the corresponding Wiki Explorer Task Pane. Null if not found.</returns>
        private Tools.CustomTaskPane GetWikiExplorer(Word.Window window)
        {
            foreach (Tools.CustomTaskPane ctp in CustomTaskPanes)
            {
                if (ctp.Title == XWikiNavigationPane.TASK_PANE_TITLE)
                {
                    if (ctp.Window == window)
                    {
                        return ctp;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Adds a XWiki task pane to each opened document.
        /// </summary>
        private void AddTaskPanes()
        {
            if (this.Application.Documents.Count > 0)
            {
                foreach (Word.Document doc in this.Application.Documents)
                {                    
                    AddTaskPane(doc);
                }
            }
        }

        /// <summary>
        /// Removes all XWiki task panes from the given document.
        /// </summary>
        /// <param name="doc"></param>
        private void RemoveTaskPane(Word.Document doc)
        {
            Word.Window ctpWindow;
            Tools.CustomTaskPane ctp;

            for (int i = this.CustomTaskPanes.Count; i > 0; i--)
            {
                ctp = this.CustomTaskPanes[i - 1];
                ctpWindow = (Word.Window)ctp.Window;
                String tag = (String)ctp.Control.Tag;
                if (tag.Contains(XWikiNavigationPane.XWIKI_EXPLORER_TAG) && ctpWindow == doc.ActiveWindow)
                {
                    this.CustomTaskPanes.Remove(ctp);
                }
            }
        }

        /// <summary>
        /// Removes all XWiki task panes.
        /// <see cref="RemoveTaskPane"/>
        /// </summary>
        private void RemoveAllTaskPanes()
        {
            try
            {
                if (this.Application.Documents.Count > 0)
                {
                    for (int i = this.CustomTaskPanes.Count; i > 0; i--)
                    {
                        Tools.CustomTaskPane ctp = this.CustomTaskPanes[i - 1];
                        String tag = (String)ctp.Control.Tag;
                        if (tag.Contains(XWikiNavigationPane.XWIKI_EXPLORER_TAG))
                        {
                            this.CustomTaskPanes.Remove(ctp);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UserNotifier.Error(ex.Message);
            }
        }

        /// <summary>
        /// Removes all task pane instances that not longer have an existing window.
        /// </summary>
        private void RemoveOrphans()
        {
            for (int i = this.CustomTaskPanes.Count; i > 0; i--)
            {
                Tools.CustomTaskPane ctp = this.CustomTaskPanes[i - 1];
                if (ctp.Window == null)
                    this.CustomTaskPanes.Remove(ctp);
            }
        }

        /// <summary>
        /// Hides/Shows the Wiki Explorer taskpanes.
        /// </summary>
        public void ToggleTaskPanes()
        {
            if (bShowTaskPanes)
                HideTaskPanes();
            else
                ShowTaskPanes();
            bShowTaskPanes = !(bShowTaskPanes);
        }

        /// <summary>
        /// Shows or hides the active Wiki Explorer.
        /// </summary>
        /// <param name="newState">The new state of the pane. TRUE for visible, FALSE for hidden.</param>
        public void ToggleActiveTaskPane(bool newState)
        {
            Tools.CustomTaskPane ctp = GetActiveWikiExplorer();
            if (ctp != null)
            {
                ctp.Visible = newState;
            }
        }

        private void SetFolderSuffix(XOfficeCommonSettings settings)
        {
            Object _false = false;
            Object _true = true;
            String defaultSuffixValue = "_files";
            Word.Document newDoc = Application.Documents.Add(ref missing, ref missing, ref missing, ref _false);

            String uniqueName = "XOffice" + DateTime.Now.Ticks.ToString();
            Object uniquePath = Path.Combine(settings.PagesRepository, uniqueName);
            
            //required by COM interop
            Object format = Word.WdSaveFormat.wdFormatHTML;
            
            newDoc.SaveAs(ref uniquePath, ref format, ref missing, ref missing, ref _false, ref missing, ref missing,
                          ref missing, ref missing, ref missing, ref missing, ref missing, ref missing, ref missing,
                          ref missing, ref missing);
            newDoc.Close(ref _false, ref missing, ref missing);
            DirectoryInfo dirInfo = new DirectoryInfo(settings.PagesRepository);
            //search for the metadata directory.
            foreach (DirectoryInfo subDir in dirInfo.GetDirectories())
            {
                if (subDir.Name.StartsWith(uniqueName))
                {
                    String suffix = subDir.Name.Replace(uniqueName, "");
                    settings.MetaDataFolderSuffix = suffix;
                    XOfficeCommonSettingsHandler.WriteRepositorySettings(settings);
                }
            }
            if (settings.MetaDataFolderSuffix == "")
            {
                settings.MetaDataFolderSuffix = defaultSuffixValue;
            }
        }

        /// <summary>
        /// Makes the login to the server, using the ConnectionSettingsForm
        /// or the last stored credentials.
        /// Adds the taskpanes.
        /// </summary>
        public void InitializeAddin()
        {
            //Set encoding to ISO-8859-1(Western)
            Application.Options.DefaultTextEncoding = Microsoft.Office.Core.MsoEncoding.msoEncodingWestern;
            Application.Options.UseNormalStyleForList = true;
            this.SaveFormat = Word.WdSaveFormat.wdFormatHTML;

            timer = new System.Timers.Timer(TIMER_INTERVAL);
            //Repositories and temporary files settings
            if (XOfficeCommonSettingsHandler.HasSettings())
            {
                addinSettings = XOfficeCommonSettingsHandler.GetSettings();                
                if (addinSettings.MetaDataFolderSuffix == "")
                {
                    SetFolderSuffix(addinSettings);
                }
            }
            else
            {
                this.AddinSettings = new XOfficeCommonSettings();                
            }            
            timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            timer.Start();

            InitializeEventsHandlers();
            bool connected = false;
            bool hasCredentials = LoadCredentials();
            if (hasCredentials && AddinSettings.AutoLogin)
            {
                Client = XWikiClientFactory.CreateXWikiClient(AddinSettings.ClientType, serverURL, username, password);
                connected = Client.LoggedIn;
            }
            else if (!hasCredentials)
            {
                //if the user wants to login at startup, and enter the credentials
                if (AddinSettings.AutoLogin)
                {
                    if (ShowConnectToServerUI())
                    {
                        connected = Client.LoggedIn;
                        this.AddinStatus.Syntax = this.DefaultSyntax;
                    }
                }
            }
            if (!connected)
            {
                Globals.Ribbons.XWikiRibbon.SwitchToOfflineMode();
            }
            else if (Client.LoggedIn)
            {
                Globals.Ribbons.XWikiRibbon.SwitchToOnlineMode();
            }

            this.AnnotationMaintainer = new AnnotationMaintainer();            

            addinActions = new AddinActions(this);
            Log.Success("XWord started");
        }

        private void InitializeEventsHandlers()
        {
            this.ClientInstanceChanged += new ClientInstanceChangedHandler(XWikiAddIn_ClientInstanceChanged);
            this.LoginFailed += new LoginFailedHandler(XWikiAddIn_LoginFailed);
            this.LoginSuccessul += new LoginSuccessfulHandler(XWikiAddIn_LoginSuccessul);
        }

        void XWikiAddIn_LoginSuccessul()
        {

            if (!HasVisibleWikiExplorer())
            {
                AddTaskPanes();
            }
            // refreshes the ribbon buttons which allow the user to work with the documents from XWiki server
            Globals.Ribbons.XWikiRibbon.Refresh(null, null);
            Globals.Ribbons.XWikiRibbon.SwitchToOnlineMode();
            XWikiNavigationPane.ReloadDataAndSyncAll();
            Log.Success("Logged in  to " + serverURL);
        }

        void XWikiAddIn_LoginFailed()
        {
            String authMessage = "Login failed!" + Environment.NewLine;
            authMessage += "Unable to login, please check your username & password." + Environment.NewLine;
            authMessage += "Hint: make sure you are using correct letter case. Username and password are case sensitive.";
            UserNotifier.StopHand(authMessage);
            Log.Error("Login to server " + serverURL + " failed");
            //Remove TaskPanes
            RemoveAllTaskPanes();
        }

        private bool ShowConnectToServerUI()
        {
            if (AddinSettingsForm.IsShown == false)
            {
                AddinSettingsForm addinSettingsForm = new AddinSettingsForm();
                new AddinSettingsFormManager(ref addinSettingsForm).EnqueueAllHandlers();
                if (addinSettingsForm.ShowDialog() == DialogResult.OK)
                {
                    return true;
                }
            }
            return false;
        }

        void XWikiAddIn_ClientInstanceChanged(object sender, EventArgs e)
        {            
            if (Client != null)
            {
                if (Client.LoggedIn)
                {
                    //notify successfull login
                    LoginSuccessul();
                }
                else
                {
                    //notify failed login
                    LoginFailed();
                }
            }
            //else toggle addinn
        }

        /// <summary>
        /// Executes when the specified amount of time has passed.
        /// </summary>
        /// <param name="e">The event parameters.</param>
        /// <param name="sender">The control that triggered the event.</param>
        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ReinforceApplicationOptions();
        }

        /// <summary>
        /// Sets the application(Word) options.
        /// </summary>
        public void ReinforceApplicationOptions()
        {
            try
            {
                //Using UnicodeLittleEndian as we read data from the disk using StreamReader
                //The .NET String has UTF16 littleEndian(Unicode) encoding.
                Application.Options.DefaultTextEncoding = Microsoft.Office.Core.MsoEncoding.msoEncodingUnicodeLittleEndian;
                Application.ActiveDocument.SaveEncoding = Microsoft.Office.Core.MsoEncoding.msoEncodingUnicodeLittleEndian;
                Application.Options.UseNormalStyleForList = true;
            }
            //Is thrown because in some cases the VSTO runtime is stopped after the word instance is closed.
            catch (COMException) { };
        }

        /// <summary>
        ///  Loads the last used credentials of the user.
        /// </summary>
        /// <returns>TRUE if the user has stored credentials can be performed. FALSE otherwise.</returns>
        private bool LoadCredentials()
        {
            LoginData loginData = new LoginData(LoginData.XWORD_LOGIN_DATA_FILENAME);
            bool hasCredentials = loginData.HasCredentials();
            if (hasCredentials)
            {
                rememberCredentials = true;
                String[] credentials = loginData.GetCredentials();
                serverURL = credentials[0];
                username = credentials[1];
                password = credentials[2];
            }
            else
            {
                rememberCredentials = false;
            }
            return hasCredentials;
        }

        /// <summary>
        /// Specifies if the addin has WikiEx
        /// </summary>
        /// <returns></returns>
        public bool HasVisibleWikiExplorer()
        {
            foreach (Tools.CustomTaskPane ctp in Globals.XWikiAddIn.XWikiCustomTaskPanes)
            {
                String tag = (String)ctp.Control.Tag;
                if (tag.Contains(XWikiNavigationPane.XWIKI_EXPLORER_TAG))
                {
                    return true;    
                }
            }
            return false;
        }

        /// <summary>
        /// Hides all WikiExplorers in all documents
        /// </summary>
        /// <param name="visible">True - makes the taskpane visible. False - Hides the taskpane.</param>
        private void ShowTaskPanes(bool visible)
        {
            RemoveOrphans();
            foreach (Tools.CustomTaskPane ctp in Globals.XWikiAddIn.XWikiCustomTaskPanes)
            {
                String tag = (String)ctp.Control.Tag;
                if (tag.Contains(XWikiNavigationPane.XWIKI_EXPLORER_TAG))
                {
                    ctp.Visible = visible;
                }
            }
        }

        /// <summary>
        /// Shows all WikiExplorer taskpanes in the addin.
        /// </summary>
        public void ShowTaskPanes()
        {
            ShowTaskPanes(true);
        }

        /// <summary>
        /// Hides all WikiExploer taskpanes in the addin.
        /// </summary>
        public void HideTaskPanes()
        {
            ShowTaskPanes(false);
        }

        #region VSTO generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InternalStartup()
        {
            this.Startup += new System.EventHandler(ThisAddIn_Startup);
            this.Shutdown += new System.EventHandler(ThisAddIn_Shutdown);
        }
        
        #endregion
    }
}
