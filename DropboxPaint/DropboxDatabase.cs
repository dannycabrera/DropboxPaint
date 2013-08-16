using System;
using DropBoxSync.iOS;
using MonoTouch.Foundation;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Drawing;

namespace DropboxPaint
{
	public class DropboxDatabase
	{
		public event EventHandler LinesUpdated;
		public event EventHandler ClearLines;

		public List<Line> DrawnLines { get; set; }
		public List<Line> AddedLines { get; set; }

		static DropboxDatabase shared;

		public String NewId
		{
			get { return Guid.NewGuid ().ToString ().Replace ("-", ""); }
		}

		public static DropboxDatabase Shared {
			get {
				if (shared == null)
					shared = new DropboxDatabase ();
				return shared;
			}
		}

		DBDatastore store;

		public DropboxDatabase ()
		{
			DrawnLines = new List<Line> ();
			AddedLines = new List<Line> ();
		}

		public void Init ()
		{
			if (store != null)
				return;
			DBError error;
			store = DBDatastore.OpenDefaultStoreForAccount (DBAccountManager.SharedManager.LinkedAccount, out error);
			store.Sync (null);
			store.AddObserver (store, () => {
				LoadData ();
			});
		}

		public Task LoadData ()
		{
			var task = Task.Factory.StartNew (() => {
				var table = store.GetTable ("lines");
				DBError error = new DBError ();

				var results = table.Query (null, out error);
				if (results.Length != 0) {
					ProccessResults (results);
				} else
					store.BeginInvokeOnMainThread (() => {
						ClearLines(this, EventArgs.Empty);
					});
			});
			return task;
		}

		void ProccessResults (DBRecord[] results)
		{
			foreach (var result in results) {
				Shared.AddedLines.Add (result.ToLine ());
			}

			store.Sync (null);

			if (Shared.AddedLines.Count > 0) {
				store.BeginInvokeOnMainThread (() => {
					LinesUpdated (this, EventArgs.Empty);
				});
			}
		}

		public void DeleteAll ()
		{
			var table = store.GetTable ("lines");
			DBError error;
			var results = table.Query (new NSDictionary (), out error);
			foreach (var result in results) {
				result.DeleteRecord ();
			}
			store.Sync (null);
			AddedLines.Clear ();
			DrawnLines.Clear ();
		}

		public void InsertLine (Line line)
		{
			var table = store.GetTable ("lines");
			line.Id = NewId;
			table.GetOrInsertRecord (line.Id, line.ToDictionaryTest (), false, new DBError ());
			DrawnLines.Add (line);
			store.Sync (null);
		}
	}

	public static class DropboxHelper
	{
		public static NSDictionary ToDictionaryTest (this Line line)
		{
			var keys = new NSString[] {
				new NSString("Color"),
				new NSString("Points")
			};
			var values = new NSString[] {
				new NSString(line.Color.ToString()),
				new NSString(Newtonsoft.Json.JsonConvert.SerializeObject (line.Points))
			};
			return NSDictionary.FromObjectsAndKeys (values, keys);
		}

		public static Line ToLine (this DBRecord record)
		{
			return new Line ().Update (record);
		}

		public static Line Update (this Line line, DBRecord record)
		{
			line.Id = record.RecordId;
			line.Color = Convert.ToInt32(record.Fields [new NSString ("Color")].ToString ());
			line.Points = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PointF>> (record.Fields [new NSString ("Points")].ToString ());
			return line;
		}
	}
}

