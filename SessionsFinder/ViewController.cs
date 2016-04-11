using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using AppKit;
using Foundation;

using MicrosoftBuildExtractor;
using DataModel;

namespace SessionsFinder
{

	public partial class ViewController : NSViewController
	{
        enum State {
            NONE,
            IDLE, LOADING, LOADED, SAVING, SAVED
        }

		public ViewController (IntPtr handle) : base (handle)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

            reflectState(State.IDLE);

			// Do any additional setup after loading the view.
            label.StringValue = "";//CBuild.BASE + CBuild.BUILD2016";
		}

		public override NSObject RepresentedObject {
			get {
				return base.RepresentedObject;
			}
			set {
				base.RepresentedObject = value;
				// Update the view, if already loaded.
			}
		}

        partial void SaveClicked (Foundation.NSObject sender) {
            reflectState(State.SAVING);

            var window = this.View.Window;

            var dlg = new NSSavePanel ();
            dlg.Title = "Save Result";
            dlg.BeginSheet (window, (rslt) => {
                // File selected?
                if (rslt == 1) {
                    var path = dlg.Url.Path;

                    // store to file
                    using (System.IO.StreamWriter file = 
                        new System.IO.StreamWriter(@path))
                    {
                        file.Write(Results.TextStorage.ToString());
                    }
                    reflectState(State.SAVED);
                }
            });
        }

        partial void ExtractClicked(Foundation.NSObject sender) {
            reflectState(State.LOADING);
            IExtractor e = new Build2016();
            label.StringValue = string.Format("... extracting data from {0}", e.GetId());

            Task.Run(() => {
                loadAndParse(e);
            });
        }

        async Task loadAndParse(IExtractor extractor) {            
            // Insert code here to initialize your application
            Dictionary<String, Session> sessions = extractor.GetSessions();
            var data = extractor.SerialiseToJson(sessions);
            var contents = new NSMutableAttributedString(data);
            InvokeOnMainThread (() => {
                Results.TextStorage.SetString(contents);
                reflectState(State.LOADED);
                label.StringValue = "Done.";
            });
        }

        private State currentState = State.NONE;

        void reflectState(State state) {
            if (currentState == state)
                return;

            switch (state) {
                case State.IDLE:
                    this.SaveButton.Enabled = false;
                    this.ExtractButton.Enabled = true;
                    this.Results.Editable = false;
                    break;
                case State.LOADING:
                    this.SaveButton.Enabled = false;
                    this.ExtractButton.Enabled = false;
                    this.Results.Editable = false;
                    break;
                case State.LOADED:
                    this.SaveButton.Enabled = true;
                    this.ExtractButton.Enabled = true;
                    this.Results.Editable = true;
                    break;
                case State.SAVING:
                    this.SaveButton.Enabled = false;
                    this.ExtractButton.Enabled = false;
                    this.Results.Editable = false;
                    break;
                case State.SAVED:
                    this.SaveButton.Enabled = true;
                    this.ExtractButton.Enabled = true;
                    this.Results.Editable = true;
                    break;
            }
            currentState = state;
        }
	}
}
