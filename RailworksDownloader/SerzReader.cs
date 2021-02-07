using SteamKit2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using static RailworksDownloader.Utils;

namespace RailworksDownloader
{
    internal class SerzReader
    {
        private abstract class Tag
        {
            public Types Type { get; set; }

            public int RWtype;
            public ushort TagNameID { get; set; }

            public enum Types : byte
            {
                MatrixTag = 65, //Matrix tag
                BlobTag = 66, //Blob tag
                MagicTag = 67, //Unknown tag
                NilTag = 78, //Nil tag
                OpenTag = 80, //Opening tag
                RefTag = 82, //Reference tag
                DataTag = 86, //Data tag
                CloseTag = 112, //Closing tag
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
                Type = Types.OpenTag;
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

        private class MagicTag : Tag
        {
            public byte UnknownByte { get; set; }

            public uint UnknownUint { get; set; }

            public MagicTag(byte unknownByte, uint unknownUint)
            {
                Type = Types.MagicTag;
                UnknownByte = unknownByte;
                UnknownUint = unknownUint;
            }
        }

        private class BlobTag : Tag
        {
            public uint Size { get; set; }
            public byte[] Data { get; set; }

            public BlobTag(uint size, byte[] data)
            {
                Type = Types.BlobTag;
                Size = size;
                Data = data;
            }
        }

        private class SerzDependency
        {
            public string Provider { get; set; }
            public string Product { get; set; }
            public string Asset { get; set; }
        }

        private const uint SERZ_MAGIC = 1515341139U;
        private const string XML_HEADER = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n";
        private const byte BINDEX_MAX = 0xFF;

        private int CurrentXMLlevel { get; set; }

        private ushort BIndex { get; set; }

        private ushort SIndex { get; set; }

        private int DebugStep { get; set; }

        private Stream InputStream { get; set; }
        private FileStream OutputStream { get; set; }

        private readonly string[] Strings = new string[0xFFFF];

        private readonly Tag[] BinTags = new Tag[BINDEX_MAX];

        private readonly List<Tag> AllTags = new List<Tag>();

        private readonly List<SerzDependency> Dependencies = new List<SerzDependency>();

        public SerzReader(string inputFile)
        {
            InputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
            Deserialize();
        }

        public SerzReader(Stream fileStream)
        {
            InputStream = fileStream;
            Deserialize();
        }

        private void Deserialize()
        {
            BinaryReader binaryReader = new BinaryReader(InputStream, Encoding.UTF8);

            if (binaryReader.BaseStream.Length > sizeof(int) * 2)
            {
                try
                {
                    //Check file contains SERZ at 0x0
                    uint magic = binaryReader.ReadUInt32();
                    if (magic == SERZ_MAGIC)
                    {
                        binaryReader.ReadInt32();

                        DebugStep = 0;
                        BIndex = 0;
                        SIndex = 0;
                        CurrentXMLlevel = 0;

                        //Read whole bin file
                        while (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
                        {
                            byte cur_byte = binaryReader.ReadByte();

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
                catch (Exception e)
                {
                    if (e.GetType() != typeof(ThreadInterruptedException) && e.GetType() != typeof(ThreadAbortException))
                        Trace.Assert(false, $"Error when parsing bin file:\n{e}");
                }
            }
        }

        private ushort ReadString(ref BinaryReader br)
        {
            ushort string_id = br.ReadUInt16(); //read two bytes as short

            Debug.Assert(string_id < SIndex || string_id == 0xFFFF, string.Format("Adding non loaded string id {0} at position {1}, step {2}!", string_id, br.BaseStream.Position, DebugStep));

            Debug.Assert(Strings.Length != 41);

            if (string_id == 0xFFFF) //if string index == FFFF then it is string itself
            {
                int string_len = br.ReadInt32(); //read string length
                char[] _s = new char[string_len];
                for (int i = 0; i < string_len; i++)
                {
                    byte b = br.ReadByte();
                    _s[i] = (char)b;
                    if (b < 0xA0)
                        continue;

                    byte[] bchar;
                    switch (b)
                    {
                        case 0xEF:
                            bchar = br.ReadBytes(2);
                            bchar[0] += 4;
                            break;
                        default:
                            bchar = new byte[2] { b, br.ReadByte() };
                            break;
                    }

                    char[] c = Encoding.UTF8.GetChars(bchar);
                    Array.Copy(c, 0, _s, i, c.Length);
                }

                string s = new string(_s);
                Strings[SIndex % 0xFFFF] = s; //saves string
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
                        string elemContent = Strings[dt.IntValue];
                        if (!string.IsNullOrWhiteSpace(elemContent))
                        {
                            switch (Strings[tagName_id])
                            {
                                case "Provider":
                                    {
                                        Dependencies.Add(new SerzDependency());
                                        Dependencies.Last().Provider = elemContent;
                                        break;
                                    }
                                case "Product":
                                    {
                                        if (Dependencies.Count > 0 && string.IsNullOrWhiteSpace(Dependencies.Last().Product))
                                            Dependencies.Last().Product = elemContent;
                                        break;
                                    }
                                case "BlueprintID":
                                    {
                                        if (Dependencies.Count > 0 && string.IsNullOrWhiteSpace(Dependencies.Last().Asset))
                                            Dependencies.Last().Asset = elemContent;
                                        break;
                                    }
                            }
                        }
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
                        Debug.Assert(false, string.Format("Unknown data type {0} at position {1}, step {2}!", Strings[format_id], br.BaseStream.Position, DebugStep));
                        return;
                        //throw new Exception(string.Format("Unknown data type {0} at position {1}, step {2}!", Strings[format_id], br.BaseStream.Position, DebugStep));
                    }
            }

            BinTags[BIndex % BINDEX_MAX] = dt;
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
                        string elemContent = Strings[dt.IntValue];
                        if (!string.IsNullOrWhiteSpace(elemContent))
                        {
                            switch (Strings[tagName_id])
                            {
                                case "Provider":
                                    {
                                        Dependencies.Add(new SerzDependency());
                                        Dependencies.Last().Provider = elemContent;
                                        break;
                                    }
                                case "Product":
                                    {
                                        if (Dependencies.Count > 0 && string.IsNullOrWhiteSpace(Dependencies.Last().Product))
                                            Dependencies.Last().Product = elemContent;
                                        break;
                                    }
                                case "BlueprintID":
                                    {
                                        if (Dependencies.Count > 0 && string.IsNullOrWhiteSpace(Dependencies.Last().Asset))
                                            Dependencies.Last().Asset = elemContent;
                                        break;
                                    }
                            }
                        }
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
            ushort format_id = ReadString(ref br); //reads format of saved data

            Debug.Assert(Strings[format_id] == "sFloat32", string.Format("Unknown format {0} in mattrice on position {1}, step {2}!", Strings[format_id], br.BaseStream.Position, DebugStep));

            ushort num_elements = br.ReadByte();
            float[] elements = new float[num_elements];

            for (int i = 0; i < num_elements; i++)
            {
                elements[i] = br.ReadSingle();
            }

            MatrixTag mt = new MatrixTag(elements, 0, tagName_id);

            BinTags[BIndex % BINDEX_MAX] = mt;
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
                case Tag.Types.OpenTag:
                    {
                        ushort tagName_id = ReadString(ref br); //reads name of tag
                        int node_id = br.ReadInt32(); //gets node id
                        int node_type = br.ReadInt32(); //gets node type

                        StartTag st = new StartTag(node_id, node_type, tagName_id);

                        BinTags[BIndex % BINDEX_MAX] = st;
                        AllTags.Add(st);

                        CurrentXMLlevel++;

                        break;
                    }
                case Tag.Types.CloseTag:
                    {
                        ushort string_id = br.ReadUInt16(); //read two bytes as short

                        EndTag et = new EndTag(string_id);

                        BinTags[BIndex % BINDEX_MAX] = et;
                        AllTags.Add(et);

                        CurrentXMLlevel--;

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

                        BinTags[BIndex % BINDEX_MAX] = nt;
                        AllTags.Add(nt);

                        break;
                    }
                case Tag.Types.RefTag:
                    {
                        ushort tagName_id = ReadString(ref br);
                        int node_id = br.ReadInt32();

                        RefTag rt = new RefTag(node_id, tagName_id);

                        BinTags[BIndex % BINDEX_MAX] = rt;
                        AllTags.Add(rt);

                        break;
                    }
                case Tag.Types.MagicTag:
                    {
                        byte unknown_byte = br.ReadByte();
                        uint unknown_uint = br.ReadUInt32();

                        MagicTag mt = new MagicTag(unknown_byte, unknown_uint);

                        BinTags[BIndex % BINDEX_MAX] = mt;

                        //Debug.Assert(false, string.Format("Magic tag at level {0}, byte {1}, uint {2}, BIndex {3}!!!", CurrentXMLlevel, unknown_byte, unknown_uint, BIndex % BINDEX_MAX));

                        break;
                    }
                case Tag.Types.BlobTag:
                    {
                        uint blobSize = br.ReadUInt32();
                        byte[] blobData = new byte[blobSize];

                        for (int i = 0; i < Math.Ceiling((double)blobSize / int.MaxValue); i++)
                        {
                            int buffer_size = (int)Math.Min(blobSize - int.MaxValue * i, int.MaxValue);
                            byte[] buffer = br.ReadBytes(buffer_size);
                            Array.Copy(buffer, 0, blobData, int.MaxValue * i, buffer_size);
                        }

                        BlobTag bt = new BlobTag(blobSize, blobData);

                        BinTags[BIndex % BINDEX_MAX] = bt;
                        AllTags.Add(bt);

                        break;
                    }
                default:
                    Debug.Assert(false, string.Format("Unknown tag format {0} at position {1}, step {2}!", command_type, br.BaseStream.Position, DebugStep));
                    break;
                    //throw new Exception(string.Format("Unknown tag format {0} at position {1}, step {2}!", command_type, br.BaseStream.Position, DebugStep));
            }
        }

        private void ParseExistingTag(byte cur_byte, ref BinaryReader br)
        {
            ref Tag refTag = ref BinTags[cur_byte];

            try
            {
                switch (refTag.Type)
                {
                    case Tag.Types.OpenTag:
                        {
                            int node_id = br.ReadInt32(); //gets node id
                            int node_type = br.ReadInt32(); //gets node type

                            AllTags.Add(new StartTag(node_id, refTag.RWtype, refTag.TagNameID));
                            CurrentXMLlevel++;
                            break;
                        }
                    case Tag.Types.CloseTag:
                        {
                            AllTags.Add(new EndTag(refTag.TagNameID));
                            CurrentXMLlevel--;
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
                    case Tag.Types.MagicTag:
                        {
                            byte unknown_byte = br.ReadByte();
                            uint unknown_uint = br.ReadUInt32();

                            MagicTag mt = new MagicTag(unknown_byte, unknown_uint);

                            //BinTags[BIndex % BINDEX_MAX] = ut;

                            //Debug.Assert(false, string.Format("Reused magic tag at level {0}, byte {1}, uint {2}, BIndex {3}!!!", CurrentXMLlevel, unknown_byte, unknown_uint, BIndex % BINDEX_MAX));

                            break;
                        }
                    case Tag.Types.BlobTag:
                        {
                            uint blobSize = br.ReadUInt32();
                            byte[] blobData = new byte[blobSize];

                            for (int i = 0; i < Math.Ceiling((double)blobSize / int.MaxValue); i++)
                            {
                                int buffer_size = (int)Math.Min(blobSize - int.MaxValue * i, int.MaxValue);
                                byte[] buffer = br.ReadBytes(buffer_size);
                                Array.Copy(buffer, 0, blobData, int.MaxValue * i, buffer_size);
                            }

                            AllTags.Add(new BlobTag(blobSize, blobData));
                            break;
                        }
                }
            }
            catch
            {
                Debug.Assert(false, $"Unable to parse position {br.BaseStream.Position}!");
            }

        }

        public string[] GetDependencies()
        {
            SerzDependency[] deps = Dependencies.Where(x => !string.IsNullOrWhiteSpace(x.Asset) && (Path.GetExtension(x.Asset.ToLower()) == ".xml" || Path.GetExtension(x.Asset.ToLower()) == ".bin")).ToArray();
            int depsCount = deps.Length;

            string[] outDeps = new string[depsCount];

            for (int i = 0; i < depsCount; i++)
            {
                ref SerzDependency serzDep = ref deps[i];
                outDeps[i] = NormalizePath(Path.Combine(serzDep.Provider, serzDep.Product, serzDep.Asset));
            }

            return outDeps;
        }

        public void FlushToXML(string outputFile)
        {
            CurrentXMLlevel = 0;
            OutputStream = File.OpenWrite(outputFile);

            WriteString(XML_HEADER);

            for (int i = 0; i < AllTags.Count; i++)
            {
                Tag currentTag = AllTags[i];
                Tag nextTag = i + 1 < AllTags.Count ? AllTags[i + 1] : null;

                Debug.Assert(currentTag.TagNameID < SIndex, $"Attempted to flush unreaded string!");

                if (i == 0)
                {
                    StringBuilder builder = new StringBuilder(Strings[currentTag.TagNameID]);
                    builder.Replace("::", "-");
                    string tag_name = builder.ToString();
                    if (tag_name.Length == 0)
                        tag_name = "e";
                    string id = ((StartTag)currentTag).ID > 0 ? string.Format("d:id =\"{0}\"", ((StartTag)currentTag).ID) : "";

                    if (nextTag?.TagNameID == currentTag.TagNameID)
                        WriteString(string.Format("<{0} xmlns:d=\"http://www.kuju.com/TnT/2003/Delta\" d:version=\"1.0\" {1}/>\r\n", tag_name, id));
                    else
                        WriteString(string.Format("<{0} xmlns:d=\"http://www.kuju.com/TnT/2003/Delta\" d:version=\"1.0\" {1}>\r\n", tag_name, id));
                    CurrentXMLlevel++;
                }
                else
                {
                    switch (currentTag.Type)
                    {
                        case Tag.Types.OpenTag:
                            {
                                StartTag st = (StartTag)currentTag;
                                StringBuilder builder = new StringBuilder(Strings[st.TagNameID]);
                                builder.Replace("::", "-");
                                string tag_name = builder.ToString();
                                if (tag_name.Length == 0)
                                    tag_name = "e";
                                if (st.ID > 0)
                                {
                                    if (nextTag?.TagNameID == currentTag.TagNameID)
                                        WriteString(string.Format("<{0} d:id=\"{1}\"/>\r\n", tag_name, st.ID));
                                    else
                                        WriteString(string.Format("<{0} d:id=\"{1}\">\r\n", tag_name, st.ID));
                                }
                                else
                                {
                                    if (nextTag?.TagNameID == currentTag.TagNameID)
                                        WriteString(string.Format("<{0}/>\r\n", tag_name));
                                    else
                                        WriteString(string.Format("<{0}>\r\n", tag_name));
                                }
                                CurrentXMLlevel++;
                                break;
                            }
                        case Tag.Types.DataTag:
                            {
                                DataTag dt = (DataTag)currentTag;
                                StringBuilder builder = new StringBuilder(Strings[dt.TagNameID]);
                                builder.Replace("::", "-");
                                string tag_name = builder.ToString();
                                if (tag_name.Length == 0)
                                    tag_name = "e";
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

                                    WriteString(string.Format("<{0} d:type=\"{3}\" d:alt_encoding=\"{1}\" d:precision=\"string\">{2}</{0}>\r\n", tag_name, BitConverter.ToString(bytes).Replace("-", string.Empty), dt.FloatValue, format));
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
                                        WriteString(string.Format("<{0} d:type=\"{1}\">{2}</{0}>\r\n", tag_name, format, HttpUtility.HtmlEncode(Strings[(int)dt.IntValue])));
                                    }
                                    else if (dt.DataType == Tag.DataTypes.Int8 || dt.DataType == Tag.DataTypes.Int16 || dt.DataType == Tag.DataTypes.Int32 || dt.DataType == Tag.DataTypes.Int64)
                                    {
                                        WriteString(string.Format("<{0} d:type=\"{1}\">{2}</{0}>\r\n", tag_name, format, (long)dt.IntValue + long.MinValue));
                                    }
                                    else
                                    {
                                        WriteString(string.Format("<{0} d:type=\"{1}\">{2}</{0}>\r\n", tag_name, format, dt.IntValue));
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
                                    string tag_name = builder.ToString();
                                    if (tag_name.Length == 0)
                                        tag_name = "e";
                                    WriteString(string.Format("</{0}>\r\n", tag_name));
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
                                string tag_name = builder.ToString();
                                if (tag_name.Length == 0)
                                    tag_name = "e";
                                WriteString(string.Format("<{0} d:numElements=\"{1}\" d:elementType=\"sFloat32\" d:precision=\"string\">{2}</{0}>\r\n", tag_name, mt.Elements.Length, inner_string.Trim()));
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
                                string tag_name = builder.ToString();
                                if (tag_name.Length == 0)
                                    tag_name = "e";
                                WriteString(string.Format("<{0} d:type=\"ref\">{1}</{0}>\r\n", tag_name, rt.ID));
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
                s = ((char)0x09) + s;
            }
            byte[] b = Encoding.UTF8.GetBytes(s);
            OutputStream.Write(b, 0, b.Length);
            OutputStream.FlushAsync();
        }
    }
}
