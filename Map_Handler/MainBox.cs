﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Globalization;
using BlamLib;
using DATA_STRUCTURES;


namespace Map_Handler
{
    public partial class MainBox : Form
    {
        #region EXTRACTION_RELATED_VARS 

        //For BlamLib tag extraction
        Dictionary<int, string> AddList;
        Dictionary<int, string> ExtractList;

        static StreamReader map_stream;//current map stream reader
        //as the name suggests
        static StreamReader mp_shared_stream;
        static StreamReader sp_shared_stream;
        static StreamReader mainmenu_stream;
        Dictionary<int, string> debug_tag_names;//a list containing tag path stuff :P

        static bool map_loaded = false;//is the map loaded

        public static string map_name = "";//name of the map along woth destination
        public static string map_dir = "";//path of the mapfile above, where we look for shared/ui etc.

        string settings_path = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.ApplicationData), "H2_PCHandlerSettings.txt");

        public static BlamLib.Test.Halo2 H2Test = new BlamLib.Test.Halo2(); //Blam Lib Tests project


        public static Dictionary<int, string> AllTagList;
        public static string H2V_BaseMapsDirectory;


        #endregion

        public MainBox()
        {
            InitializeComponent();

            AddList = new Dictionary<int, string>();
            ExtractList = new Dictionary<int, string>();
        }

        #region NON_BLAM_LIB_EXTRACTION

        private void openMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Map opening stuff
            OpenFileDialog map_file = new OpenFileDialog();
            map_file.Filter = "Halo 2 Vista Map (*.map)|*.map";

            if (map_file.ShowDialog() == DialogResult.OK)
            {
                LoadResourceMaps();
                map_stream = new StreamReader(map_file.FileName);

                debug_tag_names = new Dictionary<int, string>();
                AllTagList = new Dictionary<int, string>();

                int table_off = DATA_READ.ReadINT_LE(0x10, map_stream);
                int table_size = DATA_READ.ReadINT_LE(0x14, map_stream);
                int file_table_offset = DATA_READ.ReadINT_LE(0x2D0, map_stream);

                int table_start = table_off + 0xC * DATA_READ.ReadINT_LE(table_off + 4, map_stream) + 0x20;

                map_name = map_file.FileName;
                map_dir = Path.GetDirectoryName(map_name);
                textBox1.Text = "Map Loaded -  " + map_name;
                textBox4.Text = "0 Tags Selected";
                initialize_treeview(table_start, table_size, file_table_offset);
                map_loaded = true;
                TagToolStripMenu.Visible = true;
                metaToolStripMenuItem.Visible = true;
                dumpTagsListToolStripMenuItem.Visible = true;
                dumpStringIDToolStripMenuItem.Visible = true;
            }
        }

        /// <summary>
        /// Initialises tree view upon opening a map file
        /// </summary>
        void initialize_treeview(int table_start,int table_size,int file_table_offset)
        {
            treeView1.Nodes.Clear();

            int tag_count = 0;
            int path_start = 0;

            for (int i = 0; ; i++)
            {
                int tag_table_REF = table_start + 0x10 * i;

                if (tag_table_REF > table_size + table_start)
                    break;

                string type = DATA_READ.ReadTAG_TYPE(tag_table_REF, map_stream);
                int datum_index = DATA_READ.ReadINT_LE(tag_table_REF + 4, map_stream);
                string path = DATA_READ.ReadSTRING(file_table_offset + path_start, map_stream);

                if (datum_index != -1)
                {
                    //lets check the mem addrs validity before adding it to the list
                    int mem_addr = DATA_READ.ReadINT_LE(tag_table_REF + (datum_index & 0xffff) * 0x10 + 8, map_stream);

                    if (mem_addr != 0x0)
                        AllTagList.Add(datum_index, path);//Adding only Map Specific tags with Internal Reference only to list 

                    if (treeView1.Nodes.IndexOfKey(type) == -1)
                    {
                        treeView1.Nodes.Add(type, "- " + type);
                    }
                    int index = treeView1.Nodes.IndexOfKey(type);

                    //HEX Values contains ABCDEF
                    treeView1.Nodes[index].Nodes.Add(tag_table_REF.ToString(), "- " + path);

                    //add this stuff to the SID list
                    debug_tag_names.Add(datum_index, path);

                    //ugh! is basically the last tag
                    if (type.CompareTo("ugh!") == 0)
                        break;

                    path_start += path.Length + 1;
                }
                tag_count = i;
            }
            treeView1.Sort();
            textBox2.Text = tag_count.ToString() + " Total Tags";
        }

        private void extractMetaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (map_loaded)
            {
                if (treeView1.SelectedNode != null)
                {
                    int tag_table_ref = Int32.Parse(treeView1.SelectedNode.Name);
                    string type = DATA_READ.ReadTAG_TYPE(tag_table_ref, map_stream);
                    int datum_index = DATA_READ.ReadINT_LE(tag_table_ref + 4, map_stream);
        
                    //Meta Extractor
                    MetaExtractor meta_extract;
                    meta_extract = new MetaExtractor(datum_index, type, map_stream, mp_shared_stream, sp_shared_stream, mainmenu_stream);
                    meta_extract.Show();                   

                }
                else MessageBox.Show("Select a TAG", "Hint");
            }
            else
            {
                MessageBox.Show("Select a map First", "Hint");
            }
        }
        private static void LoadResourceMaps()
        {
            string mainmenu = H2V_BaseMapsDirectory + "\\mainmenu.map";
            string shared = H2V_BaseMapsDirectory + "\\shared.map";
            string spshared = H2V_BaseMapsDirectory + "\\single_player_shared.map";

            mainmenu_stream = new StreamReader(mainmenu);
            mp_shared_stream = new StreamReader(shared);
            sp_shared_stream = new StreamReader(spshared);
        }
        private static void UnLoadResourceMaps()
        {
            mainmenu_stream.Close();
            mp_shared_stream.Close();
            sp_shared_stream.Close();
        }
        //function display the tag structure
        private void getTagStructureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (map_loaded)
            {
                if (treeView1.SelectedNode != null)
                {
                    string type = DATA_READ.ReadTAG_TYPE(Int32.Parse(treeView1.SelectedNode.Name), map_stream);

                    plugins_field temp = DATA_READ.Get_Tag_stucture_from_plugin(type);
                    if (temp != null)
                    {
                        TreeNode tn = temp.Get_field_structure();

                        tn.Text = type;

                        treeView1.Nodes.Clear();
                        treeView1.Nodes.Add(tn);
                    }
                    else MessageBox.Show("The plugin of type " + type + " doesn't exist", "ERROR");

                    map_loaded = false;
                }
                else MessageBox.Show("Select a TAG", "Hint");
            }
            else
            {
                MessageBox.Show("Select a map First", "Hint");
            }
        }

        private void CompileMetaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //File dailogue to select the config file
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "config files(*.xml)|*.xml";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                Rebase_meta RM = new Rebase_meta(ofd.FileName);
                RM.Show();
            }
        }

        #endregion

        #region BLAM_LIB_EXTRACTION

        //Tag extraction stuff
        public static void CloseMap()
        {
            if (map_loaded)
            {
                UnLoadResourceMaps();
                map_stream.Close();
                map_loaded = false;
            }
        }

        public static void ReOpenMap()
        {
            if (!map_loaded)
            {
                LoadResourceMaps();
                map_stream = new StreamReader(map_name);
                map_loaded = true;
            }
        }

        public void UnCheckAll(TreeNodeCollection nodes)
        {

            foreach (System.Windows.Forms.TreeNode tagitem in nodes)
            {
                tagitem.Checked = false;
                if (tagitem.Nodes.Count != 0)
                    UnCheckAll(tagitem.Nodes);
            }
        }

        public void CheckedTags(TreeNodeCollection nodes)
        {

            foreach (System.Windows.Forms.TreeNode tagitem in nodes)
            {

                if (tagitem.Checked)
                {
                    if (tagitem.Level != 0)
                    {
                        int tag_table_ref = Int32.Parse(tagitem.Name);
                        int datum_index = DATA_READ.ReadINT_LE(tag_table_ref + 4, map_stream);
                        if (!AddList.ContainsKey(datum_index))
                            AddList.Add(datum_index, tagitem.Text);

                    }
                    else
                        CheckedTags(tagitem.Nodes);
                }
                if (tagitem.Nodes.Count != 0)
                    CheckedTags(tagitem.Nodes);
            }
            textBox4.Text = AddList.Count.ToString() + " Tags Selected";
        }

        private void extractTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (map_loaded)
            {
                AddList.Clear();
                CheckedTags(treeView1.Nodes);

                AddTags();
            }
            else
            {
                MessageBox.Show("No Map Loaded ,Reload it", "Error!!", MessageBoxButtons.OK);
            }
        }

        private void AddTags()
        {
            progressBar1.Value = 0;

            if (AddList == null)
                return; //EROROORR

            for (int o = 0; o < AddList.Count; o++)
            {
                if (!ExtractList.ContainsKey(AddList.ElementAt(o).Key))
                {
                    ExtractList.Add(AddList.ElementAt(o).Key, AddList.ElementAt(o).Value);
                    richTextBox1.AppendText("[" + AddList.ElementAt(o).Key.ToString("X") + "] " + AddList.ElementAt(o).Value + "\n");
                }
            }
            extract_button.Enabled = true;
            clear_button.Enabled = true;
            AddList.Clear();
            UnCheckAll(treeView1.Nodes);
            label4.Text = ExtractList.Count.ToString() + " Tags Added";
            textBox4.Text = "0 Tags Selected";
        }

        private void decompileMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (map_loaded)
            {
                AddList = AllTagList;
                AddTags();
            }
            extract_button.Enabled = true;
            clear_button.Enabled = true;
        }       

        #endregion

        private void closeMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CloseMap();
            treeView1.Nodes.Clear();
            textBox1.Text = "";
            textBox2.Text = "";
            textBox3.Text = "";
            richTextBox1.Text = "";
            TagToolStripMenu.Visible = false;            
            progressBar1.Value = 0;
            dumpTagsListToolStripMenuItem.Visible = false;
            dumpStringIDToolStripMenuItem.Visible = false;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CloseMap();
            Application.Exit();
        }

        private void defaultMaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            setMapsFolder();
        }

        private void setMapsFolder()
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = false;

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                H2V_BaseMapsDirectory = fbd.SelectedPath;

                MessageBox.Show(H2V_BaseMapsDirectory);
                System.IO.File.WriteAllText(settings_path, H2V_BaseMapsDirectory);
            }

            if (!map_loaded)
            {
                textBox1.Text = "Maps Folder - " + H2V_BaseMapsDirectory;
            }

        }

        void DumpTagList()
        {
            if (treeView1.SelectedNode.Nodes.Count != 0)
            {
                string[] x = new string[treeView1.SelectedNode.Nodes.Count];
                int i = 0;
                foreach (TreeNode tn in treeView1.SelectedNode.Nodes)
                {
                    int tag_table_ref = Int32.Parse(tn.Name);
                    int datum_index = DATA_READ.ReadINT_LE(tag_table_ref + 4, map_stream);

                    string Name = System.IO.Path.GetFileNameWithoutExtension(tn.Text);

                    x[i++] = Name + "," + "0x" + datum_index.ToString("X"); ;
                }

                File.WriteAllLines(Application.StartupPath + @"\AddList.txt", x);
            }
            else
            {
                MessageBox.Show("Select Tag Nodes First", "Error", MessageBoxButtons.OK);
            }
        }

        void WIP()
        {
            MessageBox.Show("Work In Progress!!", "WIP", MessageBoxButtons.OK);
        }

        private void hCEGBXModelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //ToDO: Complete this with UI
            // WIP();                   
        }

        private void hCEToH2VSoundsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //ToDO: Complete this with UI
            WIP();
        }

        private void hCECollisionModelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //ToDO: Complete this with UI
            WIP();
        }

        private void ExtractImportlStripMenuItem_Click(object sender, EventArgs e)
        {
            String BrowseDirectory = "";
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = false;

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BrowseDirectory = fbd.SelectedPath;
                H2Test.Halo2TestImportInfoExtraction(BrowseDirectory, "");
                MessageBox.Show("Done ", "Progress", MessageBoxButtons.OK);
            }
        }

        private void dumpSelectedTagsListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DumpTagList();
            MessageBox.Show("Done ", "Progress", MessageBoxButtons.OK);
        }

        private void tests1ToolStripMenuItem_Click(object sender, EventArgs e)
        {

            //H2Test.Halo2TestCacheOutputPc();
        }

        private void resyncshadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Resyncer_Dialog_Box RDB = new Resyncer_Dialog_Box();
            RDB.Show();
        }

        private void resyncStringIDsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "config files(*.xml)|*.xml";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                Resync_SID RSID = new Resync_SID(ofd.FileName);
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.openMapToolStripMenuItem_Click(sender, e);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.closeMapToolStripMenuItem_Click(sender, e);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.extractTagToolStripMenuItem_Click(sender, e);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.decompileMapToolStripMenuItem_Click(sender, e);
        }

        //Code from VB/C# example documents. "TreeView.AfterCheck Event"

        private void CheckAllChildNodes(TreeNode treeNode, bool nodeChecked)
        {
            foreach (TreeNode node in treeNode.Nodes)
            {
                node.Checked = !nodeChecked;
                if (node.Nodes.Count > 0)
                {
                    // If the current node has child nodes, call the CheckAllChildsNodes method recursively.
                    this.CheckAllChildNodes(node, nodeChecked);
                }
            }
        }

        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Action != TreeViewAction.Unknown)
            {

                if (e.Node.Nodes.Count > 0)
                {

                    this.CheckAllChildNodes(e.Node, !e.Node.Checked);
                }
                AddList.Clear();
                CheckedTags(treeView1.Nodes);
            }

        }

        private void extract_button_Click(object sender, EventArgs e)
        {

            string DestinationFolder = textBox3.Text;

            string mapName = DATA_READ.Read_File_from_file_location(MainBox.map_name);

            current_tag_status.Visible = true;

            if (DestinationFolder == "")
            {
                current_tag_status.Text = "Select a Destination Folder Please";
                return;
            }

            current_tag_status.Text = "Initializing Decompiler";
            MainBox.CloseMap();

            List<int> extract_list = ExtractList.Keys.ToList<int>();
            MainBox.H2Test.Halo2_ExtractTagCache(extract_list, recursive_radio_.Checked, output_db_.Checked, override_tags_.Checked, DestinationFolder, map_dir + "\\", ref progressBar1, mapName);
            /*
            progressBar1.Value = 0;
            progressBar1.Maximum = ExtractList.Count;
            int index = 0;
            foreach (int i in ExtractList.Keys)
            {
                
                current_tag_status.Text = "Extracting Objects : " + ExtractList.Values.ElementAt(index);
                MainBox.H2Test.Halo2_ExtractTagCache(i, isRecursive, isOutDBOn, isOverrideOn, DestinationFolder, H2V_BaseMapsDirectory + "\\", mapName);
                progressBar1.Value++; //update the progress bar
                index++;
            }
            */

            current_tag_status.Text = "Extraction Complete!";
            if (MessageBox.Show("Extraction Done!", "Progress", MessageBoxButtons.OK) == DialogResult.OK)
            {
                MainBox.ReOpenMap();
            }
            clear_button.Enabled = false;
            extract_button.Enabled = false;
            ExtractList.Clear();
            richTextBox1.Text = "";
        }

        private void clear_button_Click(object sender, EventArgs e)
        {
            clear_button.Enabled = false;
            extract_button.Enabled = false;
            ExtractList.Clear();
            richTextBox1.Text = "";
            label4.Text = "";
        }

        private void MainBox_Load(object sender, EventArgs e)
        {
            ForceMaps();
        }

        private void ForceMaps()
        {
            if (File.Exists(settings_path))
            {
                H2V_BaseMapsDirectory = File.ReadAllText(settings_path);
                textBox1.Text = "Maps Folder - " + H2V_BaseMapsDirectory;
            }
            else
            {
                MessageBox.Show("Please select your Maps Folder");
                setMapsFolder();
                ForceMaps();
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox3.Text = fbd.SelectedPath;

            }
        }

        private void sndtagFixesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "config files(*.xml)|*.xml";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                snd_fixes snd = new snd_fixes(ofd.FileName);
            }
        }

        private void EmulateShaderDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShaderEmulator shad = new ShaderEmulator();
            shad.EmulateShaderDumpToolStripMenuItem_Click(sender, e);

        }

        private void CreatePluginsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Elmer Source Code
            // <3 Hamp

            string tagdirectory = "";

            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;

            MessageBox.Show("Please select a directory containing .shader_template files. These will be converted to plugins for the shader emulator.");

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                tagdirectory = fbd.SelectedPath;

                var stfiles = Directory.GetFiles(tagdirectory, $"*.shader_template", SearchOption.AllDirectories);
                var stfiles2 = Directory.GetFiles(tagdirectory, $"*.shader_template", SearchOption.AllDirectories);
                string outpath = Path.Combine(Environment.CurrentDirectory, "plugins", "shaderstemplates");
                string outpath2 = Path.Combine(Environment.CurrentDirectory, "plugins", "pixeltemplates");
                Directory.CreateDirectory(outpath);
                Directory.CreateDirectory(outpath2);

                foreach (var stfile2 in stfiles2)
                {
                    int magic = new int();

                    int propcount = new int();
                    int catcount = new int();

                    List<string> pixelID = new List<string>();

                    using (var fs = new FileStream(stfile2, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                    using (var ms = new MemoryStream())
                    using (var bw = new BinaryWriter(ms))
                    using (var br = new BinaryReader(ms))
                    {
                        fs.CopyTo(ms);
                        ms.Position = 0;

                        br.BaseStream.Seek(80, SeekOrigin.Begin);
                        magic = magic + br.ReadInt16();

                        br.BaseStream.Seek(108, SeekOrigin.Begin);
                        propcount = br.ReadInt16();

                        br.BaseStream.Seek(120, SeekOrigin.Begin);
                        catcount = br.ReadInt16();

                        int[] catskipcount = new int[catcount];   // we use this as a collection of both description text length
                                                                  // as well as any other random data of indeterminate length 
                                                                  // along the way that we need to skip reading over. Sloppy! <3 Hamp
                        int[] parcount = new int[catcount];

                        br.BaseStream.Seek(236 + magic, SeekOrigin.Begin);

                        if (propcount > 0)
                        {
                            br.BaseStream.Seek(16, SeekOrigin.Current);
                            magic = magic + 16;
                        }

                        for (int i = 0; i < propcount; i++)
                        {
                            br.BaseStream.Seek(7, SeekOrigin.Current);
                            magic = magic + br.ReadByte();
                        }

                        magic = magic + (propcount * 8);

                        br.BaseStream.Seek(236 + magic, SeekOrigin.Begin);
                        //MessageBox.Show(br.BaseStream.Position.ToString() + "test");

                        if (catcount > 0)
                        {
                            br.BaseStream.Seek(16, SeekOrigin.Current);
                            magic = magic + 16;
                        }

                        for (int i = 0; i < catcount; i++)
                        {
                            br.BaseStream.Seek(3, SeekOrigin.Current);
                            catskipcount[i] = catskipcount[i] + br.ReadByte();
                            parcount[i] = br.ReadByte();

                            if (parcount[i] == 0)
                            {
                                parcount[i] = 1;
                            }
                            br.BaseStream.Seek(11, SeekOrigin.Current);
                        }

                        magic = magic + (catcount * 16);

                        br.BaseStream.Seek(236 + magic, SeekOrigin.Begin);

                        for (int i = 0; i < catcount; i++)
                        {
                            br.BaseStream.Seek(catskipcount[i], SeekOrigin.Current);
                            br.BaseStream.Seek(16, SeekOrigin.Current);

                            int[] descriptionlength = new int[parcount[i]];
                            int[] namelength = new int[parcount[i]];
                            int[] tagpath = new int[parcount[i]];
                            int[] ID = new int[parcount[i]];
                            for (int n = 0; n < parcount[i]; n++)
                            {
                                namelength[n] = 0;
                                br.BaseStream.Seek(2, SeekOrigin.Current);

                                byte[] flip = br.ReadBytes(2);
                                Array.Reverse(flip);
                                namelength[n] = BitConverter.ToInt16(flip, 0);

                                descriptionlength[n] = br.ReadInt16();

                                br.BaseStream.Seek(18, SeekOrigin.Current);
                                ID[n] = br.ReadInt16();

                                br.BaseStream.Seek(10, SeekOrigin.Current);
                                tagpath[n] = br.ReadInt16();

                                br.BaseStream.Seek(34, SeekOrigin.Current);
                            }

                            for (int n = 0; n < parcount[i]; n++)
                            {

                                if (ID[n] >= 1)
                                {
                                    //MessageBox.Show(br.BaseStream.Position.ToString() + "--nl" + namelength[n]);
                                    pixelID.Add(new string(br.ReadChars(namelength[n])));
                                    //MessageBox.Show("Wrote-" + bitmapID[bitmapID.Count - 1]);
                                }

                                else
                                {
                                    br.BaseStream.Seek(namelength[n], SeekOrigin.Current);
                                    //MessageBox.Show("Skipped-"+namelength[n].ToString());
                                }
                                //MessageBox.Show(br.BaseStream.Position.ToString() + "--ps" + descriptionlength[n]);
                                br.BaseStream.Seek(descriptionlength[n], SeekOrigin.Current);

                                if (tagpath[n] > 0)
                                {
                                    // MessageBox.Show(br.BaseStream.Position.ToString() + "--tp" + tagpath[n] + 1);
                                    br.BaseStream.Seek(tagpath[n] + 1, SeekOrigin.Current);
                                }
                            }
                        }

                        string output2 = Path.Combine(outpath2, Path.GetFileName(stfile2) + "_pixel.txt");

                        System.IO.File.WriteAllText(output2, "");

                        foreach (var pixel in pixelID)
                        {
                            //  MessageBox.Show(bitm);
                            System.IO.File.AppendAllText(output2, pixel + Environment.NewLine);
                        }
                    }
                }
                foreach (var stfile in stfiles)
                {
                    int magic = new int();

                    int propcount = new int();
                    int catcount = new int();

                    List<string> bitmapID = new List<string>();

                    using (var fs = new FileStream(stfile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                    using (var ms = new MemoryStream())
                    using (var bw = new BinaryWriter(ms))
                    using (var br = new BinaryReader(ms))
                    {
                        fs.CopyTo(ms);
                        ms.Position = 0;

                        br.BaseStream.Seek(80, SeekOrigin.Begin);
                        magic = magic + br.ReadInt16();

                        br.BaseStream.Seek(108, SeekOrigin.Begin);
                        propcount = br.ReadInt16();

                        br.BaseStream.Seek(120, SeekOrigin.Begin);
                        catcount = br.ReadInt16();

                        int[] catskipcount = new int[catcount];   // we use this as a collection of both description text length
                                                                  // as well as any other random data of indeterminate length 
                                                                  // along the way that we need to skip reading over. Sloppy! <3 Hamp
                        int[] parcount = new int[catcount];

                        br.BaseStream.Seek(236 + magic, SeekOrigin.Begin);

                        if (propcount > 0)
                        {
                            br.BaseStream.Seek(16, SeekOrigin.Current);
                            magic = magic + 16;
                        }

                        for (int i = 0; i < propcount; i++)
                        {
                            br.BaseStream.Seek(7, SeekOrigin.Current);
                            magic = magic + br.ReadByte();
                        }

                        magic = magic + (propcount * 8);

                        br.BaseStream.Seek(236 + magic, SeekOrigin.Begin);
                        //MessageBox.Show(br.BaseStream.Position.ToString() + "test");

                        if (catcount > 0)
                        {
                            br.BaseStream.Seek(16, SeekOrigin.Current);
                            magic = magic + 16;
                        }

                        for (int i = 0; i < catcount; i++)
                        {
                            br.BaseStream.Seek(3, SeekOrigin.Current);
                            catskipcount[i] = catskipcount[i] + br.ReadByte();
                            parcount[i] = br.ReadByte();

                            if (parcount[i] == 0)
                            {
                                parcount[i] = 1;
                            }
                            br.BaseStream.Seek(11, SeekOrigin.Current);
                        }

                        magic = magic + (catcount * 16);

                        br.BaseStream.Seek(236 + magic, SeekOrigin.Begin);

                        for (int i = 0; i < catcount; i++)
                        {
                            br.BaseStream.Seek(catskipcount[i], SeekOrigin.Current);
                            br.BaseStream.Seek(16, SeekOrigin.Current);

                            int[] descriptionlength = new int[parcount[i]];
                            int[] namelength = new int[parcount[i]];
                            int[] tagpath = new int[parcount[i]];
                            int[] ID = new int[parcount[i]];
                            for (int n = 0; n < parcount[i]; n++)
                            {
                                namelength[n] = 0;
                                br.BaseStream.Seek(2, SeekOrigin.Current);

                                byte[] flip = br.ReadBytes(2);
                                Array.Reverse(flip);
                                namelength[n] = BitConverter.ToInt16(flip, 0);

                                descriptionlength[n] = br.ReadInt16();

                                br.BaseStream.Seek(18, SeekOrigin.Current);
                                ID[n] = br.ReadInt16();

                                br.BaseStream.Seek(10, SeekOrigin.Current);
                                tagpath[n] = br.ReadInt16();

                                br.BaseStream.Seek(34, SeekOrigin.Current);
                            }

                            for (int n = 0; n < parcount[i]; n++)
                            {

                                if (ID[n] == 0)
                                {
                                    //MessageBox.Show(br.BaseStream.Position.ToString() + "--nl" + namelength[n]);
                                    bitmapID.Add(new string(br.ReadChars(namelength[n])));
                                    //MessageBox.Show("Wrote-" + bitmapID[bitmapID.Count - 1]);
                                }

                                else
                                {
                                    br.BaseStream.Seek(namelength[n], SeekOrigin.Current);
                                    //MessageBox.Show("Skipped-"+namelength[n].ToString());
                                }
                                //MessageBox.Show(br.BaseStream.Position.ToString() + "--ps" + descriptionlength[n]);
                                br.BaseStream.Seek(descriptionlength[n], SeekOrigin.Current);

                                if (tagpath[n] > 0)
                                {
                                    // MessageBox.Show(br.BaseStream.Position.ToString() + "--tp" + tagpath[n] + 1);
                                    br.BaseStream.Seek(tagpath[n] + 1, SeekOrigin.Current);
                                }
                            }
                        }
                        string output = Path.Combine(outpath, Path.GetFileName(stfile) + ".txt");

                        System.IO.File.WriteAllText(output, "");

                        foreach (var bitm in bitmapID)
                        {
                            //  MessageBox.Show(bitm);
                            System.IO.File.AppendAllText(output, bitm + Environment.NewLine);
                        }
                    }
                }

                string blankshader = Path.Combine(outpath, "blank.shader");

                using (var fs = new FileStream(blankshader, FileMode.Create, FileAccess.ReadWrite))
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                using (var br = new BinaryReader(ms))

                {
                    fs.CopyTo(ms);
                    ms.Position = 0;

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Encoding.UTF8.GetBytes("dahs"));

                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(64));

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(-16777215));

                    bw.Write(Encoding.UTF8.GetBytes("!MLBdfbt"));

                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(1));

                    bw.Write(Convert.ToInt32(128));

                    bw.Write(Encoding.UTF8.GetBytes("mets"));

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(-1));

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(-1));

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(-1));

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(-1));

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(-1));

                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Encoding.UTF8.GetBytes("tils"));

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(-1));

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    bw.Write(Convert.ToInt32(-1));

                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));
                    bw.Write(Convert.ToInt32(0));

                    ms.Position = 0;
                    ms.CopyTo(fs);
                }
                MessageBox.Show("Finished Writing Plugins");
            }
        }

        private void DumpShadersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            {
                if (map_loaded)
                {
                    FolderBrowserDialog fbd = new FolderBrowserDialog();
                    fbd.ShowNewFolderButton = true;

                    MessageBox.Show("Select a directory to export the shader dump. Preferably an empty folder.");

                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string tags_directory = fbd.SelectedPath;

                        StreamWriter log = new StreamWriter(tags_directory + '\\' + map_name.Substring(map_name.LastIndexOf('\\') + 1) + ".shader_log");

                        //StringId list to dump maerial name
                        List<StringID_info> StringID_list = new List<StringID_info>();

                        int string_table_count = DATA_READ.ReadINT_LE(0x170, map_stream);
                        int string_index_table_offset = DATA_READ.ReadINT_LE(0x178, map_stream);
                        int string_table_offset = DATA_READ.ReadINT_LE(0x17C, map_stream);

                        for (int index = 0; index < string_table_count; index++)
                        {
                            int table_off = DATA_READ.ReadINT_LE(string_index_table_offset + index * 0x4, map_stream) & 0xFFFF;
                            string STRING = DATA_READ.ReadSTRING(string_table_offset + table_off, map_stream);

                            if (STRING.Length > 0)
                            {
                                int SID = DATA_READ.Generate_SID(index, 0x0, STRING);//set is 0x0 cuz i couldnt figure out any other value

                                StringID_info SIDI = new StringID_info();
                                SIDI.string_index_table_index = string_index_table_offset + index * 0x4;
                                SIDI.string_table_offset = table_off;
                                SIDI.StringID = SID;
                                SIDI.STRING = STRING;

                                StringID_list.Add(SIDI);
                            }
                        }

                        foreach (TreeNode element in treeView1.Nodes["shad"].Nodes)
                        {
                            int table_ref = Int32.Parse(element.Name);
                            int datum = DATA_READ.ReadINT_LE(table_ref + 4, map_stream);
                            int mem_off = DATA_READ.ReadINT_LE(table_ref + 8, map_stream);
                            int size = DATA_READ.ReadINT_LE(table_ref + 0xc, map_stream);

                            meta meta_obj = new meta(datum, debug_tag_names[datum], map_stream);
                            meta_obj.Rebase_meta(0x0);

                            if (meta_obj.Get_Total_size() != 0)
                            {
                                byte[] meta_data = meta_obj.Generate_meta_data();

                                string text_path = tags_directory + '\\' + debug_tag_names[datum] + ".txt";

                                //lets create our directory
                                System.IO.Directory.CreateDirectory(DATA_READ.ReadDirectory_from_file_location(text_path));

                                StreamWriter sw = new StreamWriter(text_path);

                                //supoosing each shad contains only one Post process block element
                                int PPB_off = DATA_READ.ReadINT_LE(0x24, meta_data);
                                int RTP_off = DATA_READ.ReadINT_LE(0x10, meta_data);

                                int stem_datum = DATA_READ.ReadINT_LE(PPB_off, meta_data);
                                int bitmap_count = DATA_READ.ReadINT_LE(PPB_off + 0x4, meta_data);
                                int bitmapB_off = DATA_READ.ReadINT_LE(PPB_off + 0x8, meta_data);
                                int pixel_const_count = DATA_READ.ReadINT_LE(PPB_off + 0xC, meta_data);
                                int pixel_const_off = DATA_READ.ReadINT_LE(PPB_off + 0x10, meta_data);
                                int vertex_const_count = DATA_READ.ReadINT_LE(PPB_off + 0x14, meta_data);
                                int vertex_const_off = DATA_READ.ReadINT_LE(PPB_off + 0x18, meta_data);

                                sw.WriteLine("### SHADER PARAMETERS ###");
                                //write the stemp path
                                string out_temp;
                                if (stem_datum != 0 && stem_datum != -1)
                                {
                                    if (debug_tag_names.TryGetValue(stem_datum, out out_temp))
                                        sw.WriteLine(debug_tag_names[stem_datum]);
                                    else sw.WriteLine("---");
                                }
                                //write the material name
                                int mat_StringId = DATA_READ.ReadINT_LE(0x8, meta_data);
                                for (int i = 0; i < StringID_list.Count; i++)
                                {
                                    if (StringID_list[i].StringID == mat_StringId)
                                    {
                                        sw.WriteLine(StringID_list[i].STRING);
                                        break;
                                    }
                                    else if (i == (StringID_list.Count - 1))
                                        sw.WriteLine("");
                                }
                                //write the flags                            
                                sw.WriteLine(BitConverter.ToInt16(meta_data, 0x16));
                                //write depth bias offset
                                sw.WriteLine(BitConverter.ToSingle(meta_data, 0x54));
                                //write depth bias slope scale
                                sw.WriteLine(BitConverter.ToSingle(meta_data, 0x58));
                                //write dynamic specular type
                                sw.WriteLine(BitConverter.ToInt16(meta_data, 0x3E));
                                //write Lightmap type
                                sw.WriteLine(BitConverter.ToInt16(meta_data, 0x40));
                                //write lightmap specular brightness
                                sw.WriteLine(BitConverter.ToSingle(meta_data, 0x44));
                                //write Lightmap Ambient Bias
                                sw.WriteLine(BitConverter.ToSingle(meta_data, 0x48));
                                //write Shader LOD Bias
                                sw.WriteLine(BitConverter.ToInt16(meta_data, 0x3C));
                                sw.WriteLine("### SHADER PARAMETERS END ###");
                                sw.WriteLine("");
                                sw.WriteLine("### BITMAPS ###");
                                //dump the bitmap names  
                                for (int i = 0; i < bitmap_count; i++)
                                {
                                    int bitm_datum = DATA_READ.ReadINT_LE(bitmapB_off + i * 0xC, meta_data);

                                    if (bitm_datum != 0 && bitm_datum != -1)
                                    {
                                        if (debug_tag_names.TryGetValue(bitm_datum, out out_temp))
                                            if (debug_tag_names[bitm_datum] == "")
                                            {
                                                sw.WriteLine(" ");
                                            }
                                            else
                                            {
                                                sw.WriteLine(debug_tag_names[bitm_datum]);
                                            }
                                        else sw.WriteLine("---");
                                    }
                                    else
                                    {
                                        sw.WriteLine(" ");
                                    }
                                }
                                sw.WriteLine("### BITMAPS END ###");
                                sw.WriteLine("");
                                sw.WriteLine("### BITMAPS INDEX ###");
                                //dump the bitmap index 
                                for (int i = 0; i < bitmap_count; i++)
                                {
                                    sw.WriteLine(BitConverter.ToInt32(meta_data, (bitmapB_off + (i * 0xC)) + 0x4)); //Write bitmap index
                                }
                                sw.WriteLine("### BITMAPS INDEX END ###");
                                sw.WriteLine("");
                                sw.WriteLine("### LIGHTMAP ###");
                                sw.WriteLine(BitConverter.ToSingle(meta_data, RTP_off + 0x1C)); //A - Write lightmap emmisive power
                                sw.WriteLine(BitConverter.ToSingle(meta_data, RTP_off + 0x10)); //R - Write lightmap emmisive color
                                sw.WriteLine(BitConverter.ToSingle(meta_data, RTP_off + 0x14)); //G - Write lightmap emmisive color
                                sw.WriteLine(BitConverter.ToSingle(meta_data, RTP_off + 0x18)); //B - Write lightmap emmisive color
                                sw.WriteLine(BitConverter.ToSingle(meta_data, RTP_off + 0x24)); // Write lightmap half life
                                sw.WriteLine(BitConverter.ToSingle(meta_data, RTP_off + 0x48)); //A - Write lightmap transparent alpha
                                sw.WriteLine(BitConverter.ToSingle(meta_data, RTP_off + 0x3C)); //R - Write lightmap transparent color
                                sw.WriteLine(BitConverter.ToSingle(meta_data, RTP_off + 0x40)); //G - Write lightmap transparent color
                                sw.WriteLine(BitConverter.ToSingle(meta_data, RTP_off + 0x44)); //B - Write lightmap transparent color

                                sw.WriteLine("### LIGHTMAP END ###");
                                sw.WriteLine("");
                                sw.WriteLine("### PIXEL CONSTANTS ###");
                                //write pixel constants with each A,R,G,B in seperate lines
                                for (int i = 0; i < pixel_const_count; i++)
                                {
                                    uint colour = BitConverter.ToUInt32(meta_data, pixel_const_off + i * 0x4);
                                    sw.WriteLine(colour >> 24);
                                    sw.WriteLine((colour >> 16) & 0x000000ff);
                                    sw.WriteLine((colour >> 8) & 0x000000ff);
                                    sw.WriteLine(colour & 0x000000ff);
                                }
                                sw.WriteLine("### PIXEL CONSTANTS END ###");
                                sw.WriteLine("");
                                sw.WriteLine("### VERTEX CONSTANTS ###");
                                for (int i = 0; i < vertex_const_count; i++)
                                {
                                    sw.WriteLine(BitConverter.ToSingle(meta_data, (vertex_const_off + (i * 0x10)) + 0xC)); // A - Write vertex constant w
                                    sw.WriteLine(BitConverter.ToSingle(meta_data, (vertex_const_off + (i * 0x10)) + 0x0)); // R - Write vertex constant l
                                    sw.WriteLine(BitConverter.ToSingle(meta_data, (vertex_const_off + (i * 0x10)) + 0x4)); // G - Write vertex constant j
                                    sw.WriteLine(BitConverter.ToSingle(meta_data, (vertex_const_off + (i * 0x10)) + 0x8)); // B - Write vertex constant k
                                }
                                sw.WriteLine("### VERTEX CONSTANTS END ###");

                                log.WriteLine(debug_tag_names[datum] + ".txt");

                                sw.Close();
                            }
                            else
                            {
                                log.WriteLine("---");
                            }
                        }
                        log.Close();
                        MessageBox.Show("Extraction Complete");
                    }
                }
                else MessageBox.Show("Load a map first");
            }
        }

        private void dumpTagListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (map_loaded)
            {
                SaveFileDialog svd = new SaveFileDialog();

                svd.FileName = map_name.Substring(map_name.LastIndexOf('\\') + 1) + "_tag_list.txt";
                svd.Filter = "Text File|*.txt";

                if (svd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    StreamWriter log = new StreamWriter(svd.FileName);

                    var key_list = debug_tag_names.Keys.ToList();

                    for (int i = 0; i < key_list.Count; i++)
                        log.WriteLine("0x" + key_list[i].ToString("X") + ',' + debug_tag_names[key_list[i]]);

                    log.Close();
                }
            }
        }

        private void dumpStringIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (map_loaded)
            {
                SaveFileDialog svd = new SaveFileDialog();

                svd.FileName = map_name.Substring(map_name.LastIndexOf('\\') + 1) + "_StringID_list.txt";
                svd.Filter = "Text File|*.txt";

                if (svd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    StreamWriter log = new StreamWriter(svd.FileName);

                    List<StringID_info> StringID_list = new List<StringID_info>();

                    int string_table_count = DATA_READ.ReadINT_LE(0x170, map_stream);
                    int string_index_table_offset = DATA_READ.ReadINT_LE(0x178, map_stream);
                    int string_table_offset = DATA_READ.ReadINT_LE(0x17C, map_stream);

                    for (int index = 0; index < string_table_count; index++)
                    {
                        int table_off = DATA_READ.ReadINT_LE(string_index_table_offset + index * 0x4, map_stream) & 0xFFFF;
                        string STRING = DATA_READ.ReadSTRING(string_table_offset + table_off, map_stream);

                        if (STRING.Length > 0)
                        {
                            int SID = DATA_READ.Generate_SID(index, 0x0, STRING);//set is 0x0 cuz i couldnt figure out any other value

                            StringID_info SIDI = new StringID_info();
                            SIDI.string_index_table_index = string_index_table_offset + index * 0x4;
                            SIDI.string_table_offset = table_off;
                            SIDI.StringID = SID;
                            SIDI.STRING = STRING;

                            StringID_list.Add(SIDI);
                        }
                    }

                    for (int i = 0; i < StringID_list.Count; i++)
                        log.WriteLine("0x" + StringID_list[i].StringID.ToString("X") + ',' + StringID_list[i].STRING);

                    log.Close();

                }
            }
        }

        private void injectMetaToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            MetaInjector meta_inj_obj = new MetaInjector();
            meta_inj_obj.Show();
        }
    }
}