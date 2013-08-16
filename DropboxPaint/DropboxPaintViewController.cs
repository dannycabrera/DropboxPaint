using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using OpenTK.Graphics.ES11;

namespace DropboxPaint
{
	public partial class DropboxPaintViewController : UIViewController
	{
		const int AccelerometerFrequency = 25;
		const float FilteringFactor = 0.1f;
		const float EraseAccelerationThreshold = 2.0f;

		static readonly TimeSpan MinEraseInterval = TimeSpan.FromSeconds (0.5);

		const float LeftMarginPadding = 10.0f;
		const float TopMarginPadding = 10.0f;
		const float RightMarginPadding = 10.0f;

		double[] myAccelerometer = new double [3];
		SoundEffect erasingSound = new SoundEffect (NSBundle.MainBundle.PathForResource ("Erase", "caf"));
		SoundEffect selectSound  = new SoundEffect (NSBundle.MainBundle.PathForResource ("Select", "caf"));
		DateTime lastTime;

		PaintingView drawingView;

		public DropboxPaintViewController () : base ("DropboxPaintViewController", null)
		{
		}

		public override void DidReceiveMemoryWarning ()
		{
			// Releases the view if it doesn't have a superview.
			base.DidReceiveMemoryWarning ();
			
			// Release any cached data, images, etc that aren't in use.
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			
			RectangleF rect = UIScreen.MainScreen.ApplicationFrame;

			this.View.BackgroundColor = UIColor.Black;

			//Create the OpenGL drawing view and add it to the window
			drawingView = new PaintingView (new RectangleF (rect.Location, rect.Size));
			this.View.AddSubview (drawingView);

			// Create a segmented control so that the user can choose the brush color.
			UISegmentedControl segmentedControl = new UISegmentedControl (new[]{
				UIImage.FromFile ("Images/Red.png"),
				UIImage.FromFile ("Images/Yellow.png"),
				UIImage.FromFile ("Images/Green.png"),
				UIImage.FromFile ("Images/Blue.png"),
				UIImage.FromFile ("Images/Purple.png"),
			});

			// Compute a rectangle that is positioned correctly for the segmented control you'll use as a brush color palette
			RectangleF frame = new RectangleF (rect.X + LeftMarginPadding, rect.Height - Color.PaletteHeight - TopMarginPadding,
			                                   rect.Width - (LeftMarginPadding + RightMarginPadding), Color.PaletteHeight);
			segmentedControl.Frame = frame;
			// When the user chooses a color, the method changeBrushColor: is called.
			segmentedControl.ValueChanged += HandleChangeBrushColor;
			segmentedControl.ControlStyle = UISegmentedControlStyle.Bar;
			// Make sure the color of the color complements the black background
			segmentedControl.TintColor = UIColor.DarkGray;
			// Set the third color (index values start at 0)
			segmentedControl.SelectedSegment = 2;
			Color.Selected = 2;

			// Add the control to the window
			this.View.AddSubview (segmentedControl);
			// Now that the control is added, you can release it
			// [segmentedControl release];

			float r, g, b;
			// Define a starting color
			Color.HslToRgb (2.0f / Color.PaletteSize, PaintingView.Saturation, PaintingView.Luminosity, out r, out g, out b);
			// Set the color using OpenGL
			GL.Color4 (r, g, b, PaintingView.BrushOpacity);
		
			// Look in the Info.plist file and you'll see the status bar is hidden
			// Set the style to black so it matches the background of the application
			//app.SetStatusBarStyle (UIStatusBarStyle.BlackTranslucent, false);
			// Now show the status bar, but animate to the style.
		//	app.SetStatusBarHidden (false, true);

			//Configure and enable the accelerometer
			UIAccelerometer.SharedAccelerometer.UpdateInterval = 1.0f / AccelerometerFrequency;
			UIAccelerometer.SharedAccelerometer.Acceleration += OnAccelerated;
		}

		private void HandleChangeBrushColor (object sender, EventArgs e)
		{
			selectSound.Play ();
			int selected = ((UISegmentedControl)sender).SelectedSegment;
			Color.Selected = selected;
			Color.ChangeBrushColor (selected);
		}

		private void OnAccelerated (object sender, UIAccelerometerEventArgs e)
		{
			#if LINQ
			myAccelerometer = new[]{e.Acceleration.X, e.Acceleration.Y, e.Acceleration.Z}
			.Select((v, i) => v * FilteringFactor + myAccelerometer [i] * (1.0f - FilteringFactor))
				.ToArray ();
			#else
			myAccelerometer [0] = e.Acceleration.X * FilteringFactor + myAccelerometer [0] * (1.0 - FilteringFactor);
			myAccelerometer [1] = e.Acceleration.Y * FilteringFactor + myAccelerometer [1] * (1.0 - FilteringFactor);
			myAccelerometer [2] = e.Acceleration.Z * FilteringFactor + myAccelerometer [2] * (1.0 - FilteringFactor);
			#endif

			// Odd; ObjC always uses myAccelerometer[0], while 
			// I'd expect myAccelerometer[0 .. 2]
			var x = e.Acceleration.X - myAccelerometer [0];
			var y = e.Acceleration.Y - myAccelerometer [0];
			var z = e.Acceleration.Z - myAccelerometer [0];

			var length = Math.Sqrt (x * x + y * y + z * z);
			if (length >= EraseAccelerationThreshold && DateTime.Now > lastTime + MinEraseInterval) {
				erasingSound.Play ();
				drawingView.Erase ();
				lastTime = DateTime.Now;

				// Delete data on Dropbox
				DropboxDatabase.Shared.DeleteAll ();
			}
		}
	}
}

