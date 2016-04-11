// WARNING
//
// This file has been generated automatically by Xamarin Studio Community to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace SessionsFinder
{
	[Register ("ViewController")]
	partial class ViewController
	{
		[Outlet]
		AppKit.NSButton ExtractButton { get; set; }

		[Outlet]
		AppKit.NSTextField label { get; set; }

		[Outlet]
		AppKit.NSTextView Results { get; set; }

		[Outlet]
		AppKit.NSButton SaveButton { get; set; }

		[Action ("ExtractClicked:")]
		partial void ExtractClicked (Foundation.NSObject sender);

		[Action ("SaveClicked:")]
		partial void SaveClicked (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (label != null) {
				label.Dispose ();
				label = null;
			}

			if (Results != null) {
				Results.Dispose ();
				Results = null;
			}

			if (ExtractButton != null) {
				ExtractButton.Dispose ();
				ExtractButton = null;
			}

			if (SaveButton != null) {
				SaveButton.Dispose ();
				SaveButton = null;
			}
		}
	}
}
