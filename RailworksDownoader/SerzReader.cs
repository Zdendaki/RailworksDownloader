using SteamKit2.GC.Dota.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace RailworksDownloader
{
    class SerzReader
    {
        private abstract class Tag
        {
            public Types Type { get; set; }

            public int RWtype;
            public ushort TagNameID { get; set; }

            public enum Types : byte
            {
                StartTag = 80, //Opening word
                CloseTag = 112, //Closing work
                DataTag = 86, //Data word
                MatrixTag = 65, //Matrix row
                NilTag = 78, //Nil word
                RefTag = 82, //Reference word
                /*DBlob = 66, //Blob word
                unk43, //Unknown word*/
            }

            public enum DataTypes : short
            {
                String,
                Int64,
                Int32,
                Int16,
                Int8,
                UInt64,
                UInt32,
                UInt16,
                UInt8,
                Float32,
                Float64,
                Boolean
            }
        }

        private class StartTag : Tag
        {
            public int ID { get; set; }

            public StartTag(int id, int rwType, ushort stringId)
            {
                Type = Types.StartTag;
                ID = id;
                RWtype = rwType;
                TagNameID = stringId;
            }
        }

        private class EndTag : Tag
        {

            public EndTag(ushort stringId)
            {
                Type = Types.CloseTag;
                TagNameID = stringId;
            }
        }

        private class DataTag : Tag
        {

            public double FloatValue { get; set; }

            public ulong IntValue { get; set; }

            public DataTypes DataType { get; set; }

            public DataTag(DataTypes dataType, ulong intVal, int rwType, ushort tagNameId)
            {
                Type = Types.DataTag;
                DataType = dataType;
                IntValue = intVal;
                RWtype = rwType;
                TagNameID = tagNameId;
            }

            public DataTag(DataTypes dataType, double floatVal, int rwType, ushort tagNameId)
            {
                Type = Types.DataTag;
                DataType = dataType;
                FloatValue = floatVal;
                RWtype = rwType;
                TagNameID = tagNameId;
            }
        }

        private class MatrixTag : Tag
        {
            public float[] Elements { get; set; }

            public DataTypes DataType { get; set; }

            public MatrixTag(float[] elements, int rwType, ushort tagNameId)
            {
                Type = Types.MatrixTag;
                DataType = DataTypes.Float32;
                Elements = elements;
                RWtype = rwType;
                TagNameID = tagNameId;
            }
        }

        private class NilTag : Tag
        {
            public NilTag()
            {
                Type = Types.NilTag;
            }
        }

        private class RefTag : Tag
        {
            public int ID { get; set; }

            public RefTag(int id, ushort stringId)
            {
                Type = Types.RefTag;
                ID = id;
                TagNameID = stringId;
            }
        }

        private const uint SERZ_MAGIC = 1515341139U;
        private const string XML_HEADER = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n";

        private int CurrentXMLlevel { get; set; }

        private ushort BIndex { get; set; }

        private ushort SIndex { get; set; }

        private int DebugStep { get; set; }

        private FileStream InputStream { get; set; }
        private FileStream OutputStream { get; set; }

        private string[] Strings = new string[0xFFFF];

        private Tag[] BinTags = new Tag[256];

        private List<Tag> AllTags = new List<Tag>();

        public SerzReader ()
        {
            InputStream = new FileStream(@"g:\Steam\steamapps\common\RailWorks\Content\Routes\bd4aae03-09b5-4149-a133-297420197357\Networks\funkcni zaloha\Tracks6 vymena rychlostniku1 funkcni.bin", FileMode.Open, FileAccess.Read);
            OutputStream = File.OpenWrite(@"g:\Steam\steamapps\common\RailWorks\Content\Routes\bd4aae03-09b5-4149-a133-297420197357\Networks\funkcni zaloha\Tracks6 vymena rychlostniku1 funkcni.xml");

            BinaryReader binaryReader = new BinaryReader(InputStream, Encoding.UTF8);

            //Check file contains SERZ at 0x0
            uint magic = binaryReader.ReadUInt32();
            if (magic == SERZ_MAGIC)
            {
                int someNumber = binaryReader.ReadInt32();

                DebugStep = 0;
                BIndex = 0;
                SIndex = 0;

                //Read whole bin file
                while (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
                {
                    byte cur_byte = binaryReader.ReadByte();

                    /*Debug.Assert(DebugStep < 644607, "Debug assert!");

                    Debug.Assert(cur_byte > 0, "Ambigous index!");*/

                    if (cur_byte == 0xFF) //FF is "startbit" for command
                    {
                        ParseNewTag(ref binaryReader);
                        BIndex++;
                    }
                    else
                    {
                        ParseExistingTag(cur_byte, ref binaryReader);
                    }
                    DebugStep++;
                }
            }
        }

        private ushort ReadString(ref BinaryReader br)
        {
            ushort string_id = br.ReadUInt16(); //read two bytes as short

            Debug.Assert(string_id < Strings.Length+1 || string_id == 0xFFFF, String.Format("Adding non loaded string id {0} at position {1}, step {2}!", string_id, br.BaseStream.Position, DebugStep));

            if (string_id == 0xFFFF) //if string index == FFFF then it is string itself
            {
                int string_len = br.ReadInt32(); //read string length
                char[] _s = br.ReadChars(string_len); //reads bytes of string len
                //string s = Encoding.UTF8.GetString(_s); //converts byte array to string
                Strings[SIndex % 0xFFFF] = new string(_s); //saves string
                string_id = (ushort)(SIndex % 0xFFFF);

                SIndex++;
            }

            return string_id;
        }

        private void ParseNewDataTag(ref BinaryReader br)
        {
            ushort tagName_id = ReadString(ref br); //reads name of tag
            ushort format_id = ReadString(ref br); //reads format of saved data

            DataTag dt;

            switch (Strings[format_id])
            {
                case "cDeltaString":
                    {
                        dt = new DataTag(DataTag.DataTypes.String, ReadString(ref br), 0, tagName_id);
                        break;
                    }
                case "sInt64":
                    {
                        dt = new DataTag(DataTag.DataTypes.Int64, (ulong)(br.ReadInt64() - long.MinValue), 0, tagName_id);
                        break;
                    }
                case "sInt32":
                    {
                        dt = new DataTag(DataTag.DataTypes.Int32, (ulong)(br.ReadInt32() - long.MinValue), 0, tagName_id);
                        break;
                    }
                case "sInt16":
                    {
                        dt = new DataTag(DataTag.DataTypes.Int16, (ulong)(br.ReadInt16() - long.MinValue), 0, tagName_id);
                        break;
                    }
                case "sInt8":
                    {
                        dt = new DataTag(DataTag.DataTypes.Int8, (ulong)(br.ReadSByte() - long.MinValue), 0, tagName_id);
                        break;
                    }
                case "sUInt64":
                    {
                        dt = new DataTag(DataTag.DataTypes.UInt64, br.ReadUInt64(), 0, tagName_id);
                        break;
                    }
                case "sUInt32":
                    {
                        dt = new DataTag(DataTag.DataTypes.UInt32, br.ReadUInt32(), 0, tagName_id);
                        break;
                    }
                case "sUInt16":
                    {
                        dt = new DataTag(DataTag.DataTypes.UInt16, br.ReadUInt16(), 0, tagName_id);
                        break;
                    }
                case "sUInt8":
                    {
                        dt = new DataTag(DataTag.DataTypes.UInt8, br.ReadByte(), 0, tagName_id);
                        break;
                    }
                case "bool":
                    {
                        dt = new DataTag(DataTag.DataTypes.Boolean, br.ReadByte(), 0, tagName_id);
                        break;
                    }
                case "sFloat32":
                    {
                        dt = new DataTag(DataTag.DataTypes.Float32, br.ReadSingle(), 0, tagName_id);
                        break;
                    }
                case "sFloat64":
                    {
                        dt = new DataTag(DataTag.DataTypes.Float64, br.ReadDouble(), 0, tagName_id);
                        break;
                    }
                default:
                    {
                        throw new Exception(string.Format("Unknown data type {0} at position {1}, step {2}!", Strings[format_id], br.BaseStream.Position, DebugStep));
                    }
            }

            BinTags[BIndex % 255] = dt;
            AllTags.Add(dt);
        }

        private void ParseExistingDataTag(byte cur_byte, ref BinaryReader br)
        {
            ref Tag refTag = ref BinTags[cur_byte];

            ushort tagName_id = refTag.TagNameID; //reads name of tag
            DataTag.DataTypes format_id = ((DataTag)refTag).DataType; //reads format of saved data

            DataTag dt;

            switch (format_id)
            {
                case DataTag.DataTypes.String:
                    {
                        dt = new DataTag(DataTag.DataTypes.String, ReadString(ref br), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.Int64:
                    {
                        dt = new DataTag(DataTag.DataTypes.Int64, (ulong)(br.ReadInt64() - long.MinValue), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.Int32:
                    {
                        dt = new DataTag(DataTag.DataTypes.Int32, (ulong)(br.ReadInt32() - long.MinValue), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.Int16:
                    {
                        dt = new DataTag(DataTag.DataTypes.Int16, (ulong)(br.ReadInt16() - long.MinValue), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.Int8:
                    {
                        dt = new DataTag(DataTag.DataTypes.Int8, (ulong)(br.ReadSByte() - long.MinValue), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.UInt64:
                    {
                        dt = new DataTag(DataTag.DataTypes.UInt64, br.ReadUInt64(), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.UInt32:
                    {
                        dt = new DataTag(DataTag.DataTypes.UInt32, br.ReadUInt32(), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.UInt16:
                    {
                        dt = new DataTag(DataTag.DataTypes.UInt16, br.ReadUInt16(), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.UInt8:
                    {
                        dt = new DataTag(DataTag.DataTypes.UInt8, br.ReadByte(), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.Boolean:
                    {
                        dt = new DataTag(DataTag.DataTypes.Boolean, br.ReadByte(), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.Float32:
                    {
                        dt = new DataTag(DataTag.DataTypes.Float32, br.ReadSingle(), 0, tagName_id);
                        break;
                    }
                case DataTag.DataTypes.Float64:
                    {
                        dt = new DataTag(DataTag.DataTypes.Float64, br.ReadDouble(), 0, tagName_id);
                        break;
                    }
                default:
                    {
                        dt = new DataTag(DataTag.DataTypes.Boolean, 0, 0, tagName_id);
                        break;
                    }
            }

            AllTags.Add(dt);
        }

        private void ParseNewMatrixTag(ref BinaryReader br)
        {
            ushort tagName_id = ReadString(ref br); //reads name of tag
            ushort format_id = br.ReadUInt16(); //reads format of saved data

            if (Strings[format_id] != "sFloat32")
                throw new Exception(String.Format("Unknown format {0} in mattrice on position {1}, step {2}!", Strings[format_id], br.BaseStream.Position, DebugStep));

            ushort num_elements = br.ReadByte();
            float[] elements = new float[num_elements];

            for (int i = 0; i < num_elements; i++)
            {
                elements[i] = br.ReadSingle();
            }

            MatrixTag mt = new MatrixTag(elements, 0, tagName_id);

            BinTags[BIndex % 255] = mt;
            AllTags.Add(mt);
        }

        private void ParseExistingMatrixTag(byte cur_byte, ref BinaryReader br)
        {
            ref Tag refTag = ref BinTags[cur_byte];
            ushort tagName_id = refTag.TagNameID; //reads name of tag

            ushort num_elements = br.ReadByte();
            float[] elements = new float[num_elements];

            for (int i = 0; i < num_elements; i++)
            {
                elements[i] = br.ReadSingle();
            }

            MatrixTag mt = new MatrixTag(elements, 0, tagName_id);
            AllTags.Add(mt);
        }

        private void ParseNewTag(ref BinaryReader br)
        {
            Tag.Types command_type = (Tag.Types)br.ReadByte(); //read command byte

            switch (command_type)
            {
                case Tag.Types.StartTag:
                    {
                        ushort tagName_id = ReadString(ref br); //reads name of tag
                        int node_id = br.ReadInt32(); //gets node id
                        int node_type = br.ReadInt32(); //gets node type

                        StartTag st = new StartTag(node_id, node_type, tagName_id);

                        BinTags[BIndex % 255] = st;
                        AllTags.Add(st);

                        break;
                    }
                case Tag.Types.CloseTag:
                    {
                        ushort string_id = br.ReadUInt16(); //read two bytes as short

                        EndTag et = new EndTag(string_id);

                        BinTags[BIndex % 255] = et;
                        AllTags.Add(et);

                        break;
                    }
                case Tag.Types.DataTag:
                    {
                        ParseNewDataTag(ref br);
                        break;
                    }
                case Tag.Types.MatrixTag:
                    {
                        ParseNewMatrixTag(ref br);
                        break;
                    }
                case Tag.Types.NilTag:
                    {
                        NilTag nt = new NilTag();

                        BinTags[BIndex % 255] = nt;
                        AllTags.Add(nt);

                        break;
                    }
                case Tag.Types.RefTag:
                    {
                        ushort tagName_id = ReadString(ref br);
                        int node_id = br.ReadInt32();

                        RefTag rt = new RefTag(node_id, tagName_id);

                        BinTags[BIndex % 255] = rt;
                        AllTags.Add(rt);

                        break;
                    }
                default:
                    throw new Exception(string.Format("Unknown tag format {0} at position {1}, step {2}!", command_type, br.BaseStream.Position, DebugStep));
            }
        }

        private void ParseExistingTag(byte cur_byte, ref BinaryReader br)
        {
            ref Tag refTag = ref BinTags[cur_byte];

            switch (refTag.Type)
            {
                case Tag.Types.StartTag:
                    {
                        int node_id = br.ReadInt32(); //gets node id
                        int node_type = br.ReadInt32(); //gets node type

                        AllTags.Add(new StartTag(node_id, refTag.RWtype, refTag.TagNameID));
                        break;
                    }
                case Tag.Types.CloseTag:
                    {
                        AllTags.Add(new EndTag(refTag.TagNameID));
                        break;
                    }
                case Tag.Types.DataTag:
                    {
                        ParseExistingDataTag(cur_byte, ref br);
                        break;
                    }
                case Tag.Types.MatrixTag:
                    {
                        ParseExistingMatrixTag(cur_byte, ref br);
                        break;
                    }
                case Tag.Types.NilTag:
                    {
                        NilTag nt = new NilTag();

                        AllTags.Add(nt);

                        break;
                    }
                case Tag.Types.RefTag:
                    {
                        int node_id = br.ReadInt32();

                        RefTag rt = new RefTag(node_id, refTag.TagNameID);

                        AllTags.Add(rt);

                        break;
                    }
            }

        }

        public void FlushToXML()
        {
            CurrentXMLlevel = 0;
            WriteString(XML_HEADER);
            for (int i = 0; i < AllTags.Count; i++)
            {
                Tag currentTag = AllTags[i];
                Tag nextTag = i+1<AllTags.Count?AllTags[i+1]:null;

                if (i == 0)
                {
                    StringBuilder builder = new StringBuilder(Strings[currentTag.TagNameID]);
                    builder.Replace("::", "-");
                    if (nextTag?.TagNameID == currentTag.TagNameID)
                        WriteString(string.Format("<{0} xmlns:d=\"http://www.kuju.com/TnT/2003/Delta\" d:version=\"1.0\" d:id=\"{1}\"/>\r\n", builder.ToString(), ((StartTag)currentTag).ID));
                    else
                        WriteString(string.Format("<{0} xmlns:d=\"http://www.kuju.com/TnT/2003/Delta\" d:version=\"1.0\" d:id=\"{1}\">\r\n", builder.ToString(), ((StartTag)currentTag).ID));
                    CurrentXMLlevel++;
                } 
                else
                {
                    switch (currentTag.Type)
                    {
                        case Tag.Types.StartTag:
                            {
                                StartTag st = (StartTag)currentTag;
                                StringBuilder builder = new StringBuilder(Strings[st.TagNameID]);
                                builder.Replace("::", "-");
                                if (st.ID > 0)
                                {
                                    if (nextTag?.TagNameID == currentTag.TagNameID)
                                        WriteString(string.Format("<{0} d:id=\"{1}\"/>\r\n", builder.ToString(), st.ID));
                                    else
                                        WriteString(string.Format("<{0} d:id=\"{1}\">\r\n", builder.ToString(), st.ID));
                                } 
                                else
                                {
                                    if (nextTag?.TagNameID == currentTag.TagNameID)
                                        WriteString(string.Format("<{0}/>\r\n", builder.ToString()));
                                    else
                                        WriteString(string.Format("<{0}>\r\n", builder.ToString()));
                                }
                                CurrentXMLlevel++;
                                break;
                            }
                        case Tag.Types.DataTag:
                            {
                                DataTag dt = (DataTag)currentTag;
                                StringBuilder builder = new StringBuilder(Strings[dt.TagNameID]);
                                builder.Replace("::", "-");
                                if (dt.DataType == Tag.DataTypes.Float32 || dt.DataType == Tag.DataTypes.Float64)
                                {
                                    byte[] bytes = BitConverter.GetBytes(dt.FloatValue);

                                    string format = "";
                                    switch (dt.DataType)
                                    {
                                        case Tag.DataTypes.Float32:
                                            format = "sFloat32"; break;
                                        case Tag.DataTypes.Float64:
                                            format = "sFloat64"; break;
                                    }

                                    WriteString(string.Format("<{0} d:type=\"{3}\" d:alt_encoding=\"{1}\" d:precision=\"string\">{2}</{0}>\r\n", builder.ToString(), BitConverter.ToString(bytes).Replace("-", string.Empty), dt.FloatValue, format));
                                }
                                else
                                {
                                    string format = "";
                                    switch (dt.DataType)
                                    {
                                        case Tag.DataTypes.Boolean:
                                            format = "bool"; break;
                                        case Tag.DataTypes.Int8:
                                            format = "sInt8"; break;
                                        case Tag.DataTypes.Int16:
                                            format = "sInt16"; break;
                                        case Tag.DataTypes.Int32:
                                            format = "sInt32"; break;
                                        case Tag.DataTypes.Int64:
                                            format = "sInt64"; break;
                                        case Tag.DataTypes.String:
                                            format = "cDeltaString"; break;
                                        case Tag.DataTypes.UInt8:
                                            format = "sUInt8"; break;
                                        case Tag.DataTypes.UInt16:
                                            format = "sUInt16"; break;
                                        case Tag.DataTypes.UInt32:
                                            format = "sUInt32"; break;
                                        case Tag.DataTypes.UInt64:
                                            format = "sUInt64"; break;
                                    }
                                    if (dt.DataType == Tag.DataTypes.String)
                                    {
                                        WriteString(string.Format("<{0} d:type=\"{1}\">{2}</{0}>\r\n", Strings[dt.TagNameID], format, Strings[(int)dt.IntValue]));
                                    } 
                                    else if (dt.DataType == Tag.DataTypes.Int8 || dt.DataType == Tag.DataTypes.Int16 || dt.DataType == Tag.DataTypes.Int32 || dt.DataType == Tag.DataTypes.Int64)
                                    {
                                        WriteString(string.Format("<{0} d:type=\"{1}\">{2}</{0}>\r\n", Strings[dt.TagNameID], format, (long)dt.IntValue + long.MinValue));
                                    }
                                    else
                                    {
                                        WriteString(string.Format("<{0} d:type=\"{1}\">{2}</{0}>\r\n", Strings[dt.TagNameID], format, dt.IntValue));
                                    }
                                }
                                break;
                            }
                        case Tag.Types.CloseTag:
                            {
                                CurrentXMLlevel--;
                                Tag prevTag = i > 0 ? AllTags[i - 1] : null;
                                if (prevTag?.TagNameID != currentTag.TagNameID)
                                {
                                    StringBuilder builder = new StringBuilder(Strings[currentTag.TagNameID]);
                                    builder.Replace("::", "-");
                                    WriteString(string.Format("</{0}>\r\n", builder.ToString()));
                                }
                                break;
                            }
                        case Tag.Types.MatrixTag:
                            {
                                MatrixTag mt = (MatrixTag)currentTag;
                                string inner_string = "";
                                for (int j = 0; j < mt.Elements.Length; j++)
                                {
                                    inner_string += string.Format("{0:0.0000000} ", mt.Elements[j]);
                                }

                                StringBuilder builder = new StringBuilder(Strings[mt.TagNameID]);
                                builder.Replace("::", "-");
                                WriteString(string.Format("<{0} d:numElements=\"{1}\" d:elementType=\"sFloat32\" d:precision=\"string\">{2}</{0}>\r\n", builder.ToString(), mt.Elements.Length, inner_string.Trim()));
                                break;
                            }
                        case Tag.Types.NilTag:
                            {
                                WriteString("<d:nil/>\r\n");
                                break;
                            }
                        case Tag.Types.RefTag:
                            {
                                RefTag rt = (RefTag)currentTag;
                                StringBuilder builder = new StringBuilder(Strings[rt.TagNameID]);
                                builder.Replace("::", "-");
                                WriteString(string.Format("<{0} d:type=\"ref\">{1}</{0}>\r\n", builder.ToString(), rt.ID));
                                break;
                            }
                    }
                }
            }

            OutputStream.Close();
        }

        private void WriteString(string s)
        {
            for (int i = 0; i < CurrentXMLlevel; i++)
            {
                s = ((char) 0x09) + s;
            }
            byte[] b = Encoding.Default.GetBytes(s);
            OutputStream.Write(b, 0, b.Length);
        }
    }
}
