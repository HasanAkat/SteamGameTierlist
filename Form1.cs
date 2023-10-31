using System;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace SteamTierList
{
    public partial class Form1 : Form
    {
        private string steamId = "76561198286671015"; // Steam kullanıcı kimliğinizi burada ekleyin
        private string apiKey = "YOUR_API_KEY"; // Steam API anahtarınızı burada ekleyin

        public Form1()
        {
            InitializeComponent();
            dataGridView1.Columns.Add("GameName", "Oyun Adı");
            dataGridView1.Columns.Add("Rating", "Puan");

            string userUrl = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={apiKey}&steamids={steamId}";

            try
            {
                using (WebClient userClient = new WebClient())
                {
                    string userJson = userClient.DownloadString(userUrl);
                    dynamic userData = JObject.Parse(userJson);

                    if (userData.response != null && userData.response.players != null)
                    {
                        string userName = userData.response.players[0].personaname;
                        welcomeLabel.Text = $"Hoş geldin {userName}";
                    }
                    else
                    {
                        MessageBox.Show("Kullanıcı adı alınamadı.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string url = $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={apiKey}&steamid={steamId}&format=json&include_appinfo=1&include_played_free_games=1";

            try
            {
                using (WebClient client = new WebClient())
                {
                    string json = client.DownloadString(url);
                    dynamic data = JObject.Parse(json);

                    dataGridView1.Rows.Clear(); // Mevcut oyunları temizle

                    if (data.response != null && data.response.games != null)
                    {
                        foreach (var game in data.response.games)
                        {
                            string gameName = game.name;
                            if (string.IsNullOrEmpty(gameName))
                            {
                                gameName = "Bilinmeyen Oyun";
                            }

                            int appId = game.appid;
                            int oyunPuan = GetGameRatingFromDatabase(appId); // Veritabanından puanı al

                            dataGridView1.Rows.Add(gameName, oyunPuan);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Oyunlar alınamadı.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private int GetGameRatingFromDatabase(int appId)
        {
            string connectionString = "YOUR_DATABASE";
            string query = "SELECT OyunPuan FROM Oyunlar WHERE OyunID = @AppID";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@AppID", appId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetInt32(0);
                        }
                    }
                }

                connection.Close();
            }

            return 0; // Puan bulunamazsa varsayılan değeri döndür
        }
    }
}
