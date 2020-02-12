using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace glTFRevitExport
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            string tabName = "Andersen Tools";
            try
            {
                a.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Do nothing.
            }
            // Add a new ribbon panel
            RibbonPanel newPanel = a.CreateRibbonPanel(tabName, "glTF Export");
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData button1Data = new PushButtonData("command",
                "GO", thisAssemblyPath, "glTFRevitExport.Command");
            PushButton pushButton1 = newPanel.AddItem(button1Data) as PushButton;
            pushButton1.LargeImage = BmpImageSource(@"glTFRevitExport.Embedded_Media.large.png");

            /////////////////////   ADD STACKED BUTTONS   ////////////////////////////////////////////
            //PushButtonData tagSData = new PushButtonData("sButton1",
            //    "Button1", thisAssemblyPath, "glTFRevitExport.class");
            //PushButtonData tagLData = new PushButtonData("sButton2",
            //    "Button2", thisAssemblyPath, "glTFRevitExport.class");

            //newPanel.AddSeparator();

            //IList<RibbonItem> stackedItems = newPanel.AddStackedItems(tagSData, tagLData);
            //if(stackedItems.Count>1)
            //{
            //    PushButton tagS = stackedItems[0] as PushButton;
            //    tagS.Image = BmpImageSource(@"glTFRevitExport.Embedded_Media.small.png");
            //    PushButton tagL = stackedItems[1] as PushButton;
            //    tagL.Image = BmpImageSource(@"glTFRevitExport.Embedded_Media.small.png");
            //}
            ///////////////////////////////////////////////////////////////////////////////////////////


            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }

        private ImageSource BmpImageSource(string embeddedPath)
        {
            System.IO.Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedPath);
            PngBitmapDecoder pngBitmapDecoder = new PngBitmapDecoder(manifestResourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            return pngBitmapDecoder.Frames[0];
        }
    }
}
