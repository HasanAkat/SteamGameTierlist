using System;
using System.Collections.Generic;
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
        private string connectionString = "Data Source=HASAN\\MSSQLSERVER01;Initial Catalog=SteamAppDB;Integrated Security=True;"; // Veritabanı bağlantı dizesini burada ekleyin
        private DataSet dataSet;

        public Form1()
        {
            InitializeComponent();

            dataSet = new DataSet();
            DataTable dataTable = new DataTable("Oyunlar");
            dataTable.Columns.Add("Select", typeof(bool));
            dataTable.Columns.Add("GameID", typeof(int));
            dataTable.Columns.Add("GameName", typeof(string));
            dataTable.Columns.Add("Rating", typeof(int));
            dataSet.Tables.Add(dataTable);
            dataGridView1.DataSource = dataSet.Tables["Oyunlar"];

            LoadGameData(); // Form başladığında verileri yükle

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
            try
            {
                string url = $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={apiKey}&steamid={steamId}&format=json&include_appinfo=1&include_played_free_games=1";

                using (WebClient client = new WebClient())
                {
                    string json = client.DownloadString(url);
                    dynamic data = JObject.Parse(json);

                    // Veritabanına kaydedilecek yeni oyunları tutmak için bir liste oluştur
                    List<Game> newGames = new List<Game>();

                    if (data.response != null && data.response.games != null)
                    {
                        foreach (var game in data.response.games)
                        {
                            int oyunId = game.appid;
                            string oyunAd = game.name;
                            if (string.IsNullOrEmpty(oyunAd))
                            {
                                oyunAd = "Bilinmeyen Oyun";
                            }

                            // Veritabanında bu oyunun zaten kayıtlı olup olmadığını kontrol et
                            if (!IsGameInDatabase(oyunId))
                            {
                                int oyunPuan = GetGameRatingFromDatabase(oyunId);

                                newGames.Add(new Game { GameID = oyunId, GameName = oyunAd, Rating = oyunPuan });
                                dataSet.Tables["Oyunlar"].Rows.Add(false, oyunId, oyunAd, oyunPuan);
                            }
                        }

                        // Yeni oyunları veritabanına eklemek için bir işlevi çağır
                        InsertGamesIntoDatabase(newGames);
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

        private void button2_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                // Sadece işaretli satırları kontrol et
                if (row.Cells["Select"].Value != null && (bool)row.Cells["Select"].Value)
                {
                    int oyunID = (int)row.Cells["GameID"].Value;
                    int newRating = (int)row.Cells["Rating"].Value; // Yeni puanı DataGridView'den al

                    // Puanı güncelleme işlemi
                    if (UpdateGameRatingInDatabase(oyunID, newRating))
                    {
                        MessageBox.Show("Puan güncellendi.");
                    }
                    else
                    {
                        MessageBox.Show("Puan güncelleme başarısız.");
                    }
                }
            }

            // DataGridView'i yenile
            RefreshDataGridView();
        }

        private void RefreshDataGridView()
        {
            // DataGridView'i temizle
            dataSet.Tables["Oyunlar"].Clear();

            // Verileri tekrar yükle
            LoadGameData();
        }

        private bool UpdateGameRatingInDatabase(int gameID, int newRating)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("UPDATE Oyunlar SET OyunPuan = @NewRating WHERE OyunID = @GameID", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@GameID", gameID);
                        command.Parameters.AddWithValue("@NewRating", newRating);

                        int affectedRows = command.ExecuteNonQuery();

                        if (affectedRows > 0)
                        {
                            // Puan güncelleme işlemi başarılıysa transaksyonu kaydet
                            transaction.Commit();
                            return true;
                        }
                    }
                }
            }

            return false; // Puan güncelleme başarısız olursa false döndür
        }

        private bool IsGameInDatabase(int gameID)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand())
                {
                    string query = "SELECT COUNT(*) FROM Oyunlar WHERE OyunID = @GameID";
                    command.Connection = connection;
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@GameID", gameID);

                    int count = (int)command.ExecuteScalar();

                    return count > 0;
                }
            }
        }


        private void InsertGamesIntoDatabase(List<Game> newGames)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    int inserted = 0;

                    foreach (Game game in newGames)
                    {
                        int oyunId = game.GameID;
                        string gameName = game.GameName;
                        int oyunPuan = game.Rating;

                        using (SqlCommand command = new SqlCommand("", connection, transaction))
                        {
                            command.Parameters.AddWithValue("@GameID", oyunId);
                            command.Parameters.AddWithValue("@GameName", gameName);
                            command.Parameters.AddWithValue("@Rating", oyunPuan);

                            command.CommandText = "INSERT INTO Oyunlar (OyunID, OyunAd, OyunPuan) VALUES (@GameID, @GameName, @Rating)";

                            inserted += command.ExecuteNonQuery();
                        }
                    }

                    // Transaksyonu kaydet
                    transaction.Commit();

                    if (inserted > 0)
                    {
                        MessageBox.Show($"{inserted} kayıt eklendi.", "Başarılı");
                    }
                }
            }
        }

        private int GetGameRatingFromDatabase(int appId)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand())
                {
                    string query = "SELECT OyunPuan FROM Oyunlar WHERE OyunID = @AppID";
                    command.Connection = connection;
                    command.CommandText = query;
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

        private void LoadGameData()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT OyunID, OyunAd, OyunPuan FROM Oyunlar";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int oyunID = reader.GetInt32(0); // OyunID'yi al
                            string gameName = reader.GetString(1);
                            int oyunPuan = reader.GetInt32(2);

                            dataSet.Tables["Oyunlar"].Rows.Add(false, oyunID, gameName, oyunPuan);
                        }
                    }

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }
    }
}
