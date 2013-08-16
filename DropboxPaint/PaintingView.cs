using System;
using OpenTK.Platform.iPhoneOS;
using OpenTK.Graphics.ES11;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Drawing;
using MonoTouch.ObjCRuntime;
using MonoTouch.OpenGLES;
using System.Runtime.InteropServices;
using MonoTouch.CoreGraphics;
using DropBoxSync.iOS;
using System.Collections.Generic;
using System.Linq;

namespace DropboxPaint
{
	public class PaintingView : iPhoneOSGameView {

		public const float BrushOpacity = 1.0f / 3.0f;
		public const int BrushPixelStep = 3;
		public const int BrushScale = 2;
		public const float Luminosity = 0.75f;
		public const float Saturation = 1.0f;

		uint brushTexture, drawingTexture;
		bool firstTouch;

		PointF Location;
		PointF PreviousLocation;
		Line _line;

		[Export ("layerClass")]
		public static Class LayerClass ()
		{
			return iPhoneOSGameView.GetLayerClass ();
		}

		public PaintingView (RectangleF frame) : base (frame)
		{
			LayerRetainsBacking = true;
			LayerColorFormat    = EAGLColorFormat.RGBA8;
			ContextRenderingApi = EAGLRenderingAPI.OpenGLES1;
			CreateFrameBuffer();
			MakeCurrent();

			var brushImage = UIImage.FromFile ("Particle.png").CGImage;
			var width = brushImage.Width;
			var height = brushImage.Height;
			if (brushImage != null) {
				IntPtr brushData = Marshal.AllocHGlobal (width * height * 4);
				if (brushData == IntPtr.Zero)
					throw new OutOfMemoryException ();
				try {
					using (var brushContext = new CGBitmapContext (brushData,
							width, width, 8, width * 4, brushImage.ColorSpace, CGImageAlphaInfo.PremultipliedLast)) {
						brushContext.DrawImage (new RectangleF (0.0f, 0.0f, (float) width, (float) height), brushImage);
					}

					GL.GenTextures (1, ref brushTexture);
					GL.BindTexture (All.Texture2D, brushTexture);
					GL.TexImage2D (All.Texture2D, 0, (int) All.Rgba, width, height, 0, All.Rgba, All.UnsignedByte, brushData);
				}
				finally {
					Marshal.FreeHGlobal (brushData);
				}
				GL.TexParameter (All.Texture2D, All.TextureMinFilter, (int) All.Linear);
				GL.Enable (All.Texture2D);
				GL.BlendFunc (All.SrcAlpha, All.One);
				GL.Enable (All.Blend);
			}
			GL.Disable (All.Dither);
			GL.MatrixMode (All.Projection);
			GL.Ortho (0, frame.Width, 0, frame.Height, -1, 1);
			GL.MatrixMode (All.Modelview);
			GL.Enable (All.Texture2D);
			GL.EnableClientState (All.VertexArray);
			GL.Enable (All.Blend);
			GL.BlendFunc (All.SrcAlpha, All.One);
			GL.Enable (All.PointSpriteOes);
			GL.TexEnv (All.PointSpriteOes, All.CoordReplaceOes, (float) All.True);
			GL.PointSize (width / BrushScale);

			Erase ();

			// Set EventHandlers
			DropboxDatabase.Shared.LinesUpdated += HandleLinesUpdated;
			DropboxDatabase.Shared.ClearLines  += HandleClearLines;
		}

		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
			GL.DeleteTextures (1, ref drawingTexture);
		}

		public void Erase ()
		{
			GL.Clear ((uint) All.ColorBufferBit);

			SwapBuffers ();
		}

		float[] vertexBuffer;
		int vertexMax = 64;

		private void RenderLineFromPoint (PointF start, PointF end)
		{
			int vertexCount = 0;
			if (vertexBuffer == null) {
				vertexBuffer = new float [vertexMax * 2];
			}
			var count = Math.Max (Math.Ceiling (Math.Sqrt ((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y)) / BrushPixelStep),
					1);
			for (int i = 0; i < count; ++i, ++vertexCount) {
				if (vertexCount == vertexMax) {
					vertexMax *= 2;
					Array.Resize (ref vertexBuffer, vertexMax * 2);
				}
				vertexBuffer [2 * vertexCount + 0] = start.X + (end.X - start.X) * (float) i / (float) count;
				vertexBuffer [2 * vertexCount + 1] = start.Y + (end.Y - start.Y) * (float) i / (float) count;
			}
			GL.VertexPointer (2, All.Float, 0, vertexBuffer);
			GL.DrawArrays (All.Points, 0, vertexCount);

			SwapBuffers ();
		}

		int dataofs = 0;

		void HandleLinesUpdated (object sender, EventArgs e)
		{
			dataofs = 0;
			PerformSelector (new Selector ("playback"), null, 0.2f);
		}

		void HandleClearLines (object sender, EventArgs e)
		{
			Erase ();
			DropboxDatabase.Shared.DeleteAll ();
		}

		[Export ("playback")]
		void Playback ()
		{
			if (DropboxDatabase.Shared.AddedLines.Count > 0) {
				Line line = DropboxDatabase.Shared.AddedLines [dataofs];
				if (line != null) {
					List<PointF> points = line.Points;
				
					if (DropboxDatabase.Shared.DrawnLines.SingleOrDefault (l => l.Id == line.Id) == null) {
						DropboxDatabase.Shared.DrawnLines.Add (line);

						Console.WriteLine ("Drawing line {0}", line.Id);

						Color.ChangeBrushColor (line.Color);
						for (int i = 0; i < points.Count - 1; i++)
							RenderLineFromPoint (points [i], points [i + 1]);
					}

					if (dataofs < DropboxDatabase.Shared.AddedLines.Count - 1) {
						dataofs ++;
						PerformSelector (new Selector ("playback"), null, 0.01f);
					}
				} else {
					Console.WriteLine ("NULL found");
					dataofs ++;
				}
			}
		}

		public override void TouchesBegan (MonoTouch.Foundation.NSSet touches, MonoTouch.UIKit.UIEvent e)
		{
			var bounds = Bounds;
			var touch = (UITouch) e.TouchesForView (this).AnyObject;
			firstTouch = true;
			Location = touch.LocationInView (this);
			Location.Y = bounds.Height - Location.Y;
			_line = new Line ();
			_line.Color = Color.Selected;

			// Change back as it might have changed on recieved line
			Color.ChangeBrushColor (Color.Selected);
		}

		public override void TouchesMoved (MonoTouch.Foundation.NSSet touches, MonoTouch.UIKit.UIEvent e)
		{
			var bounds = Bounds;
			var touch = (UITouch) e.TouchesForView (this).AnyObject;

			if (firstTouch) {
				firstTouch = false;
				PreviousLocation = touch.PreviousLocationInView (this);
				PreviousLocation.Y = bounds.Height - PreviousLocation.Y;
			}
			else {
				Location = touch.LocationInView (this);
				Location.Y = bounds.Height - Location.Y;
				PreviousLocation = touch.PreviousLocationInView (this);
				PreviousLocation.Y = bounds.Height - PreviousLocation.Y;
			}

			if (_line.Points == null)
				_line.Points = new List<PointF> ();

			_line.Points.Add (new PointF (Location.X, Location.Y));

			RenderLineFromPoint (PreviousLocation, Location);
		}

		public override void TouchesEnded (MonoTouch.Foundation.NSSet touches, MonoTouch.UIKit.UIEvent e)
		{
			var bounds = Bounds;
			var touch = (UITouch) e.TouchesForView (this).AnyObject;
			if (firstTouch) {
				firstTouch = false;
				PreviousLocation = touch.PreviousLocationInView (this);
				PreviousLocation.Y = bounds.Height - PreviousLocation.Y;
				RenderLineFromPoint (PreviousLocation, Location);
				if (_line.Points == null)
					_line.Points = new List<PointF> ();

				_line.Points.Add (new PointF (Location.X, Location.Y));
			}

			DropboxDatabase.Shared.InsertLine (_line);
		}

		public override void TouchesCancelled (MonoTouch.Foundation.NSSet touches, MonoTouch.UIKit.UIEvent e)
		{
		}
	}
}

