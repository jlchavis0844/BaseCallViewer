using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Media;
using System.Windows.Forms;
using unirest_net.http;

namespace BaseCallDownloader {
    public partial class BaseCallsDownloader : Form {
        public static string token = "";
        public SoundPlayer sSound;
        public FileStream fileStream;
        public string fileName = "";
        public int currentRow = -1;
        public string lastID = "";
        public string callID = "";
        public DateTime made_at;

        public BaseCallsDownloader() {
            InitializeComponent();
            var fs = new FileStream(@"C:\apps\NiceOffice\token", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            using (var sr = new StreamReader(fs)) {
                token = sr.ReadToEnd();
            }
            cbUsers.DataSource = SetOwnerData().DefaultView;
            cbUsers.DisplayMember = "value";
            cbUsers.ValueMember = "key";



        }

        public static DataTable SetOwnerData() {
            System.Data.DataTable values = new DataTable();
            values.Columns.Add("key", typeof(int));
            values.Columns.Add("value", typeof(string));

            string testJSON = Get(@"https://api.getbase.com/v2/users?per_page=100&sort_by=created_at&status=active", token);
            JObject jObj = JObject.Parse(testJSON) as JObject;
            JArray jArr = jObj["items"] as JArray;
            foreach (var obj in jArr) {
                var data = obj["data"];
                int tID = Convert.ToInt32(data["id"]);
                string tName = data["name"].ToString();
                values.Rows.Add(tID, tName);
            }
            return values;

        }

        public static string Get(string url, string token) {
            string body = "";
            try {
                HttpResponse<string> jsonReponse = Unirest.get(url)
                    .header("accept", "application/json")
                    .header("Authorization", "Bearer " + token)
                    .asJson<string>();
                body = jsonReponse.Body.ToString();
                return body;
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                return body;
            }
        }

        public void GetFile(string url, string callID) {
            if (url == "" && url == null)
                return;
            fileName = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".wav";
            fileStream = File.Create(fileName);
            Unirest.get(url).asBinary().Raw.CopyTo(fileStream);
            fileStream.Close();
            lblStatus.Text = "Playing...";
            sSound = new SoundPlayer(fileName);
            sSound.Play();
        }

        private void btnFetch_Click(object sender, EventArgs e) {
            LoadCalls();
        }

        private void LoadCalls() {
            DataTable dtCalls = new DataTable();
            SqlConnection conn = new SqlConnection("Data Source=RALIMSQL1;Initial Catalog=CAMSRALFG;Integrated Security=SSPI;");
            string totalSQL = "SELECT * FROM Base_Calls WHERE user_id = "+ cbUsers.SelectedValue +" and CONVERT(varchar(10),[made_at],101) = '" +
                dtpDate.Value.ToString("MM/dd/yyyy") + "' AND [duration] <> 0 order by made_at desc";
            SqlCommand cmd = new SqlCommand(totalSQL, conn);
            conn.Open();
            SqlDataAdapter adapter = new SqlDataAdapter(cmd);
            adapter.Fill(dtCalls);
            dgvCalls.DataSource = dtCalls.DefaultView;
        }

        private void dgvCalls_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            callID = dgvCalls.Rows[e.RowIndex].Cells[0].Value.ToString();
            made_at = Convert.ToDateTime(dgvCalls.Rows[e.RowIndex].Cells[10].Value.ToString());
            if (lastID == "" || callID != lastID) { // check if same call
                lastID = callID;
                string rec_url = FetchFileURL(callID);
                lblStatus.Text = "Downloading";
                GetFile(rec_url, callID);
                btnPlay.Enabled = true;
                btnSave.Enabled = true;
            } else {//same call, replay
                sSound.Stop();
                sSound.Play();
            }
        }

        private string FetchFileURL(string callID) {
            string rawData = Get(@"https://api.getbase.com/v2/calls/" + callID, token);
            JObject jsonObj = JObject.Parse(rawData) as JObject;
            string rec_url = jsonObj["data"]["recording_url"].ToString();
            return rec_url;
        }

        private void btnPlay_Click(object sender, EventArgs e) {
            sSound.Stop();
            sSound.Dispose();
            if(fileName != "" && fileName != null && File.Exists(fileName)) {
                System.Diagnostics.Process.Start(fileName);
            }
        }

        private void dgvCalls_CellClick(object sender, DataGridViewCellEventArgs e) {
            if(e.RowIndex != currentRow) {
                if(sSound != null)
                    sSound.Dispose();
                if(fileStream != null)
                    fileStream.Close();
                fileName = "";
                currentRow = e.RowIndex;
            }
        }

        private void btnSave_Click(object sender, EventArgs e) {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            if(sSound != null) {
                sSound.Stop();
                sSound.Dispose();
            }
            saveFileDialog1.Filter = "wav files | *.wav";
            saveFileDialog1.FileName = callID + "_" + cbUsers.Text.Replace(" ", "")
                + "_" + made_at.ToString("MMddyyyy_hhmmss") + ".wav"; 
            saveFileDialog1.FilterIndex = 2;
            saveFileDialog1.RestoreDirectory = true;

            if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
                if (File.Exists(fileName)) {
                    File.Copy(fileName, saveFileDialog1.FileName,true);
                    fileName = saveFileDialog1.FileName;
                    if(MessageBox.Show("Do you want to play the file?", "Play File?", MessageBoxButtons.YesNo) == DialogResult.Yes) {
                        System.Diagnostics.Process.Start(fileName);
                    }

                }
            }
        }

        private void btnStop_Click(object sender, EventArgs e) {
            lblStatus.Text = "Stopped";
            if(sSound != null)
                sSound.Stop();
        }

        private void btnUpdate_Click(object sender, EventArgs e) {
            if (File.Exists(@"C:\apps\NiceOffice\BaseCallTracker\BaseCallTracker.exe")) {
                var process = System.Diagnostics.Process.Start(@"C:\apps\NiceOffice\BaseCallTracker\BaseCallTracker.exe");
                process.WaitForExit();
                LoadCalls();
            } else {
                MessageBox.Show("You are missing the Base Call Tracker, ask James to install it for you.",
                    "Missing required plugins" 
                    ,MessageBoxButtons.OK);
            }
        }
    }
}
