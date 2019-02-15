using System;
using System.Drawing;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using System.IO;
using System.Web.Script.Serialization;
using System.Linq;

using Red;


namespace Black
{
    public partial class Black2Json : Form
    {
        private enum ConvStatus
        {
            SUCCESS,
            ERROR,
            NOBLACKFILE
        }

        public Black2Json()
        {
            InitializeComponent();
        }

        private void ConvertFileBtn_Click(object sender, EventArgs e)
        {
            DialogResult res = openFileDialog.ShowDialog();
            ConvStatus currConvStatus = ConvStatus.ERROR;

            if (res == DialogResult.OK)
            {
                currConvStatus = ConvertFile(openFileDialog.FileName);
                UpdateStatus(currConvStatus);
            }
        }

        private void ConvertFileBtn_DragEnter(object sender, DragEventArgs e)
        {
            if(e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        private void ConvertFileBtn_DragDrop(object sender, DragEventArgs e)
        {
            bool blackfound = false;
            ConvStatus currConvStatus = ConvStatus.SUCCESS;
            string[] currStringArray = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            foreach (string currFilename in currStringArray)
            {
                if (currFilename.EndsWith(".black"))
                {
                    blackfound = true;

                    // A single error in a list of *.black files sets the status to ERROR
                    if (ConvStatus.ERROR == ConvertFile(currFilename))
                        currConvStatus = ConvStatus.ERROR;
                }
            }

            if (false == blackfound)
                currConvStatus = ConvStatus.NOBLACKFILE;

            UpdateStatus(currConvStatus);
        }

        private void UpdateStatus(ConvStatus currStatus)
        {
            if (ConvStatus.SUCCESS == currStatus)
            {
                toolStripStatus.Text = "Conversion successful!";
                toolStripStatus.BackColor = Color.LightGreen;
            }
            else if (ConvStatus.ERROR == currStatus)
            {
                toolStripStatus.Text = "Conversion error!";
                toolStripStatus.BackColor = Color.Red;
            }
            else if (ConvStatus.NOBLACKFILE == currStatus)
            {
                toolStripStatus.Text = "No *.black file!";
                toolStripStatus.BackColor = Color.Red;
            }

            timerStatusReset.Enabled = true;
        }

        private ConvStatus ConvertFile(string currFilename)
        {
            ConvStatus currConvStatus = ConvStatus.ERROR;

            using (var istream = File.OpenRead(currFilename))
            {
                using (var br = new BinaryReader(istream))
                {
                    BlackObject root = new BlackObject(istream);
                    string outputfile = Path.ChangeExtension(currFilename, "json");

                    using (var streamwriter = new StreamWriter(File.OpenWrite(outputfile)))
                    {
                        var serializer = new JavaScriptSerializer();
                        serializer.MaxJsonLength *= 2; //to make it compatible with res_model_effect3_superweapon_m_doomsday.black coz it's huge
                        serializer.RegisterConverters(new JavaScriptConverter[] { new BlackObject.BlackObjectJSONConverter() });

                        if(checkBoxDecompress.Checked)
                            streamwriter.WriteLine(JsonHelper.FormatJson(serializer.Serialize(root)));
                        else
                            streamwriter.WriteLine(serializer.Serialize(root));

                        currConvStatus = ConvStatus.SUCCESS;
                    }
                }

                return currConvStatus;
            }
        }

        private void timerStatusReset_Tick(object sender, EventArgs e)
        {
            toolStripStatus.Text = "Idle...";
            toolStripStatus.BackColor = Color.WhiteSmoke;
            timerStatusReset.Enabled = false;
        }
    }


    // JSON BEAUTIFIER THAT PARSES THE STRING ABOVE
    // Contribution from "Roc Wieler"
    class JsonHelper
    {
        private const string INDENT_STRING = "    ";
        public static string FormatJson(string str)
        {
            var indent = 0;
            var quoted = false;
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < str.Length; i++)
            {
                var ch = str[i];
                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            Enumerable.Range(0, ++indent).ForEach(item => sb.Append(INDENT_STRING));
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            sb.AppendLine();
                            Enumerable.Range(0, --indent).ForEach(item => sb.Append(INDENT_STRING));
                        }
                        sb.Append(ch);
                        break;
                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        var index = i;
                        while (index > 0 && str[--index] == '\\')
                            escaped = !escaped;
                        if (!escaped)
                            quoted = !quoted;
                        break;
                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            Enumerable.Range(0, indent).ForEach(item => sb.Append(INDENT_STRING));
                        }
                        break;
                    case ':':
                        sb.Append(ch);
                        if (!quoted)
                            sb.Append(" ");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> ie, Action<T> action)
        {
            foreach (var i in ie)
            {
                action(i);
            }
        }
    }
}

namespace Red
{
    public partial class BlackObject
    {
        public class BlackObjectJSONConverter : JavaScriptConverter
        {
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
            {
                throw new NotImplementedException();
            }
            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
            {
                return (obj as BlackObject).dictionary;
            }
            public override IEnumerable<Type> SupportedTypes
            {
                get { return new ReadOnlyCollection<Type>(new Type[] { typeof(BlackObject) }); }
            }
        }
    }
}