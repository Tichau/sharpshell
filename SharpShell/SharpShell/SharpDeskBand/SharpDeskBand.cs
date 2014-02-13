﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using Microsoft.Win32;
using SharpShell.Attributes;
using SharpShell.Components;
using SharpShell.Extensions;
using SharpShell.Interop;
using System.Windows.Forms;
using SharpShell.ServerRegistration;

namespace SharpShell.SharpDeskBand
{
    [ServerType(ServerType.ShellDeskBand)]
    public abstract class SharpDeskBand : SharpShellServer, IDeskBand2, IPersistStream, IObjectWithSite
    {
        protected SharpDeskBand()
        {
            //  Log key events.
            Log("SharpDeskBand constructed.");

            //  Create the lazy deskband provider.
            lazyDeskBand = new Lazy<UserControl>(CreateDeskBand);
        }

        //  TODO: Optionally Implement IContextMenu to support a context menu for the band.
        //  TODO: Optionally Implement IInputObject to allow input

        /// <summary>
        /// The COM site (see IObjectWithSite implementation).
        /// </summary>
        private IInputObjectSite inputObjectSite;

        /// <summary>
        /// The handle to the parent window site.
        /// </summary>
        private IntPtr parentWindowHandle;
        
        /// <summary>
        /// The band ID provided by explorer to identify the band.
        /// </summary>
        private uint explorerBandId = 0;

        #region Implmentation of the IObjectWithSite interface

        int IObjectWithSite.GetSite(ref Guid riid, out object ppvSite)
        {
            //  Log key events.
            Log("IObjectWithSite.GetSite called.");

            //  Provide the site.
            ppvSite = inputObjectSite;

            //  Got the site successfully.
            return WinError.S_OK;
        }

        int IObjectWithSite.SetSite(object pUnkSite)
        {
            //  Log key events.
            Log("IObjectWithSite.SetSite called.");

            //  If we have a site, free it. This won't actually release the COM
            //  interface until garbage collection happens.
            inputObjectSite = null;

            //  If we have not been provided a site, the band is being removed.
            if (pUnkSite == null)
            {                
                OnBandRemoved();
                lazyDeskBand.Value.Dispose();
                lazyDeskBand = new Lazy<UserControl>(CreateDeskBand);
                return WinError.S_OK;
            }

            //  We've been given a site, that means we can get the site window.
            try
            {
                //  Get the OLE window.
                var oleWindow = (IOleWindow)pUnkSite;

                //  Get the parent window handle.
                if (oleWindow.GetWindow(out parentWindowHandle) != WinError.S_OK)
                {
                    LogError("Failed to get the handle to the site window.");
                    return WinError.E_FAIL;
                }

                //  Create the desk band user interface by getting the lazy band.
                var band = lazyDeskBand.Value;

                //  Set the parent.
                User32.SetParent(band.Handle, parentWindowHandle);
            }
            catch(Exception exception)
            {
                LogError("Failed to cast the provided site to an IOleWindow.", exception);
                return WinError.E_FAIL;
            }

            //  Store the input site.
            inputObjectSite = (IInputObjectSite)pUnkSite;

            return WinError.S_OK;
        }

        #endregion

        #region Implementation of IPersistStream

        int IPersistStream.GetClassID(out Guid pClassID)
        {
            //  Log key events.
            Log("IPersistStream.GetClassID called.");

            //  Return the server class id.
            pClassID = ServerClsid;

            //  Return success.
            return WinError.S_OK;
        }

        int IPersistStream.IsDirty()
        {
            //  Log key events.
            Log("IPersistStream.IsDirty called.");

            //  TODO: return S_OK to indicate the object has changed
            //  since the last time is was saved to a stream.

            //  Until we need explorer bar persistence, we're not dirty.
            return WinError.S_FALSE;
        }

        int IPersistStream.Load(System.Runtime.InteropServices.ComTypes.IStream pStm)
        {
            //  Log key events.
            Log("IPersistStream.Load called.");

            //  Not implemented: Explorer provided Persistence.
            return WinError.S_OK;
        }

        int IPersistStream.Save(System.Runtime.InteropServices.ComTypes.IStream pStm, bool fClearDirty)
        {
            //  Log key events.
            Log("IPersistStream.Save called.");

            //  Not implemented: Explorer provided Persistence.
            return WinError.S_OK;
        }

        int IPersistStream.GetSizeMax(out ulong pcbSize)
        {
            //  Log key events.
            Log("IPersistStream.GetSizeMax called.");

            //  Not implemented: Explorer provided Persistence.
            pcbSize = 0;
            return WinError.S_OK;
        }
        
        int IPersist.GetClassID(out Guid pClassID)
        {
            //  Log key events.
            Log("IPersistStream.GetClassID called.");

            //  The class ID is just a unique identifier for the class, meaning
            //  that we can use the class GUID as it will be provided for
            //  all SharpShell servers.
            pClassID = ServerClsid;

            //  Return success.
            return WinError.S_OK;
        }

        #endregion

        #region Implmentation of IDeskBand

        int IOleWindow.GetWindow(out IntPtr phwnd)
        {
            //  Log key events.
            Log("IOleWindow.GetWindow called.");

            //   Easy enough, just return the handle of the deskband content.
            phwnd = lazyDeskBand.Value.Handle;

            //  Return success.
            return WinError.S_OK;
        }
        int IDeskBand2.GetWindow(out IntPtr phwnd)
        {
            return ((IOleWindow) this).GetWindow(out phwnd);
        }

        int IDeskBand.GetWindow(out IntPtr phwnd)
        {
            return ((IOleWindow)this).GetWindow(out phwnd);
        }

        int IDeskBand.GetBandInfo(uint dwBandID, DESKBANDINFO.DBIF dwViewMode, ref DESKBANDINFO pdbi)
        {
            //  Log key events.
            Log("IDeskBand.GetBandInfo called.");

            //  Store the band id.
            explorerBandId = dwBandID;

            //  Depending on what we've been asked for, we'll return various band properties.
            var bandOptions = GetBandOptions();
            var bandUi = lazyDeskBand.Value;

            //  Return the min size if needed.
            if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_MINSIZE))
            {
                var minSize = GetMinimumSize();
                pdbi.ptMinSize.X = minSize.Width;
                pdbi.ptMinSize.Y = minSize.Height;
            }

            //  Return the max size if needed.
            if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_MAXSIZE))
            {
                var maxSize = GetMinimumSize();
                pdbi.ptMaxSize.X = maxSize.Width;
                pdbi.ptMaxSize.Y = maxSize.Height;
            }

            if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_INTEGRAL))
            {
                //  Set the integral.
                pdbi.ptIntegral.Y = (int)bandOptions.VerticalSizingIncrement;
            }

            if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_ACTUAL))
            {
                //  Return the ideal size.
                var idealSize = bandUi.Size;
                pdbi.ptActual.X = idealSize.Width;
                pdbi.ptActual.Y = idealSize.Height;
            }

            if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_TITLE))
            {
                //  Set the title.
                if (bandOptions.ShowTitle)
                {
                    pdbi.wszTitle = bandUi.Text;
                }
                else
                {
                    pdbi.dwMask &= ~DESKBANDINFO.DBIM.DBIM_TITLE;
                }
            }

            if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_BKCOLOR))
            {
                if (bandOptions.UseBackgroundColour)
                {
                    pdbi.wszTitle = bandUi.Text;
                    pdbi.crBkgnd = new COLORREF(bandUi.BackColor);
                }
                else
                {
                    pdbi.dwMask &= ~DESKBANDINFO.DBIM.DBIM_BKCOLOR;
                }
            }

            if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_MODEFLAGS))
            {
                //  Set the flags.
                pdbi.dwModeFlags = DESKBANDINFO.DBIMF.DBIMF_NORMAL;
                if (bandOptions.HasVariableHeight) pdbi.dwModeFlags |= DESKBANDINFO.DBIMF.DBIMF_VARIABLEHEIGHT;
                if (bandOptions.IsSunken) pdbi.dwModeFlags |= DESKBANDINFO.DBIMF.DBIMF_DEBOSSED;
                if (bandOptions.UseBackgroundColour) pdbi.dwModeFlags |= DESKBANDINFO.DBIMF.DBIMF_BKCOLOR;
            }
                        
            //  Return success.
            return WinError.S_OK;
        }
        int IDeskBand2.GetBandInfo(uint dwBandID, DESKBANDINFO.DBIF dwViewMode, ref DESKBANDINFO pdbi)
        {
            return ((IDeskBand)this).GetBandInfo(dwBandID, dwViewMode, ref pdbi);
        }

        int IOleWindow.ContextSensitiveHelp(bool fEnterMode)
        {
            return WinError.E_NOTIMPL;
        }
        int IDeskBand.ContextSensitiveHelp(bool fEnterMode)
        {
            return ((IOleWindow) this).ContextSensitiveHelp(fEnterMode);
        }
        int IDeskBand2.ContextSensitiveHelp(bool fEnterMode)
        {
            return ((IOleWindow)this).ContextSensitiveHelp(fEnterMode);
        }

        int IDockingWindow.ShowDW(bool bShow)
        {
            //  Log key events.
            Log("IDockingWindow.ShowDW called.");

            //  If we've got a content window, show it or hide it.
            if(bShow)
                lazyDeskBand.Value.Show();
            else 
                lazyDeskBand.Value.Hide();

            //  Return success.
            return WinError.S_OK;
        }
        int IDeskBand.ShowDW(bool bShow) { return ((IDockingWindow)this).ShowDW(bShow); }
        int IDeskBand2.ShowDW(bool bShow) { return ((IDockingWindow)this).ShowDW(bShow); }

        int IDockingWindow.CloseDW(uint dwReserved)
        {
            //  Log key events.
            Log("IDockingWindow.CloseDW called.");

            //  If we've got a content window, hide it and then destroy it.
            if (lazyDeskBand.IsValueCreated)
            {
                lazyDeskBand.Value.Hide();
                lazyDeskBand.Value.Dispose();
                lazyDeskBand = new Lazy<UserControl>(CreateDeskBand);
            }

            //  Return success.
            return WinError.S_OK;
        }

        int IDeskBand.CloseDW(uint dwReserved) { return ((IDockingWindow)this).CloseDW(dwReserved); }
        int IDeskBand2.CloseDW(uint dwReserved) { return ((IDockingWindow)this).CloseDW(dwReserved); }

        int IDockingWindow.ResizeBorderDW(RECT rcBorder, IntPtr punkToolbarSite, bool fReserved)
        {
            //  Log key events.
            Log("IDockingWindow.ResizeBorderDW called.");

            //  This function is not used for Window's Desk Bands and in an IDeskBand implementation
            //  should always return E_NOTIMPL.
            return WinError.E_NOTIMPL;
        }
        int IDeskBand.ResizeBorderDW(RECT rcBorder, IntPtr punkToolbarSite, bool fReserved) 
        {
            return ((IDockingWindow) this).ResizeBorderDW(rcBorder, punkToolbarSite, fReserved);
        }

        int IDeskBand2.ResizeBorderDW(RECT rcBorder, IntPtr punkToolbarSite, bool fReserved)
        {
            return ((IDockingWindow)this).ResizeBorderDW(rcBorder, punkToolbarSite, fReserved);
        }

        int IDeskBand2.CanRenderComposited(out bool pfCanRenderComposited)
        {
            Log("IDeskBand2.CanRenderComposited called.");

            //  We don't support transluceny.
            pfCanRenderComposited = true;
            return WinError.S_OK;
        }

        int IDeskBand2.SetCompositionState(bool fCompositionEnabled)
        {
            Log("IDeskBand2.SetCompositionState called.");
            return WinError.S_OK;
        }

        int IDeskBand2.GetCompositionState(out bool pfCompositionEnabled)
        {
            Log("IDeskBand2.GetCompositionState called.");
            pfCompositionEnabled = false;
            return WinError.S_OK;
        }

        #endregion

        #region Custom Registration and Unregistration

        /// <summary>
        /// The custom registration function.
        /// </summary>
        /// <param name="serverType">Type of the server.</param>
        /// <param name="registrationType">Type of the registration.</param>
        /// <exception cref="System.InvalidOperationException">
        /// Unable to register a SharpNamespaceExtension as it is missing it's junction point definition.
        /// or
        /// Cannot open the Virtual Folder NameSpace key.
        /// or
        /// Failed to create the Virtual Folder NameSpace extension.
        /// or
        /// Cannot open the class key.
        /// or
        /// An exception occured creating the ShellFolder key.
        /// </exception>
        [CustomRegisterFunction]
        internal static void CustomRegisterFunction(Type serverType, RegistrationType registrationType)
        {
           //   Use the category manager to register this server as a Desk Band.
           CategoryManager.RegisterComCategory(serverType.GUID, CategoryManager.CATID_DeskBand);
        }

        /// <summary>
        /// Customs the unregister function.
        /// </summary>
        /// <param name="serverType">Type of the server.</param>
        /// <param name="registrationType">Type of the registration.</param>
        [CustomUnregisterFunction]
        internal static void CustomUnregisterFunction(Type serverType, RegistrationType registrationType)
        {
            //   Use the category manager to unregister this server as a Desk Band.
            CategoryManager.UnregisterComCategory(serverType.GUID, CategoryManager.CATID_DeskBand);
        }

        #endregion

        /// <summary>
        /// Gets the minimum size of the Band UI. This uses the <see cref="Control.MinimumSize"/> value or
        /// <see cref="Control.Size"/> if no minimum size is defined. This can be overriden to customise this
        /// behaviour.
        /// </summary>
        /// <returns>The minimum size of the Band UI.</returns>
        protected virtual Size GetMinimumSize()
        {
            //  Get the band.
            var band = lazyDeskBand.Value;

            //  Return the minimum size if none zero, otherwise the actual size.
            return new Size(band.MinimumSize.Width > 0 ? band.MinimumSize.Width : band.Width,
                band.MinimumSize.Height > 0 ? band.MinimumSize.Height : band.Height);
        }

        /// <summary>
        /// Gets the maximum size of the Band UI. This uses the <see cref="Control.MaximumSize"/> value or
        /// <see cref="Control.Size"/> if no maximum size is defined. This can be overriden to customise this
        /// behaviour.
        /// </summary>
        /// <returns>The minimum size of the Band UI.</returns>
        protected virtual Size GetMaximumSize()
        {   
            //  Get the band.
            var band = lazyDeskBand.Value;

            //  Return the minimum size if none zero, otherwise the actual size.
            return new Size(band.MaximumSize.Width > 0 ? band.MaximumSize.Width : band.Width,
                band.MaximumSize.Height > 0 ? band.MaximumSize.Height : band.Height);
        }

        /// <summary>
        /// Called when the band is being removed from explorer.
        /// </summary>
        protected virtual void OnBandRemoved()
        {
        }

        /// <summary>
        /// This function should return a new instance of the desk band's user interface,
        /// which will simply be a usercontrol.
        /// </summary>
        /// <returns></returns>
        protected abstract UserControl CreateDeskBand();

        /// <summary>
        /// Gets the band options.
        /// </summary>
        /// <returns>The band options. See <see cref="BandOptions"/> for more details.</returns>
        protected abstract BandOptions GetBandOptions();

        /// <summary>
        /// The lazy desk band provider.
        /// </summary>
        private Lazy<UserControl> lazyDeskBand;
    }
}