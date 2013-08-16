using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using DropBoxSync.iOS;
using System.Threading.Tasks;
using System.Threading;

namespace DropboxPaint
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the 
	// User Interface of the application, as well as listening (and optionally responding) to 
	// application events from iOS.
	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate
	{
		const string DropboxSyncKey = "YOUR_APP_KEY";
		const string DropboxSyncSecret = "YOUR_APP_SECRET";

		// class-level declarations
		UIWindow window;
		DropboxPaintViewController viewController;

		//
		// This method is invoked when the application has loaded and is ready to run. In this 
		// method you should instantiate the window, load the UI into it and then make the window
		// visible.
		//
		// You have 17 seconds to return from this method, or iOS will terminate your application.
		//
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			var manager = new DBAccountManager (DropboxSyncKey, DropboxSyncSecret);
			DBAccountManager.SharedManager = manager;

			window = new UIWindow (UIScreen.MainScreen.Bounds);
			
			viewController = new DropboxPaintViewController ();
			window.RootViewController = viewController;

			Task.Factory.StartNew (() => {
				this.BeginInvokeOnMainThread (() => {
					var account = DBAccountManager.SharedManager.LinkedAccount;
					if (account != null) {
						SetupDropbox ();
					} else
						manager.LinkFromController (window.RootViewController);
				});
			});

			window.MakeKeyAndVisible ();
			
			return true;
		}

		public override bool OpenUrl (UIApplication application, NSUrl url, string sourceApplication, NSObject annotation)
		{
			var account = DBAccountManager.SharedManager.HandleOpenURL (url);
			SetupDropbox ();
			return account != null;
		}

		void SetupDropbox ()
		{
			var t = Task.Factory.StartNew (() => {
				DropboxDatabase.Shared.Init ();
			});
		}
	}
}

