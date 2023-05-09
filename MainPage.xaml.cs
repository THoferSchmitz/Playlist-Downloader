using AngleSharp;
using AngleSharp.Dom;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
//using CoreImage;
using LiteDB;
using Microsoft.Maui;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static System.Net.WebRequestMethods;

namespace Ö3_Playlist
{

    public partial class SongEntry : ObservableObject
    {
        [BsonId]
        public string Name { get; set; }

        [ObservableProperty]
        public int isLiked;

        [RelayCommand]
        private void Clicked()
        {
            if (isLiked == 0) IsLiked = 1;
            else if (isLiked == 1) IsLiked = -1;
            else IsLiked = 0;
            Database.Save(this);
        }

        [RelayCommand]
        private async void Open()
        {
            string uri = @"https://www.youtube.com/results?search_query=" + Name;
            await BrowserOpen_Clicked(uri);
        }

        private async Task BrowserOpen_Clicked(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception ex)
            {
                // An unexpected error occurred. No browser may be installed on the device.
            }
        }
    }

    public class WebLoader
    {
        public IConfiguration AngleSharpConfiguration { get; set; }
        public IBrowsingContext AngleSharpContext { get; set; }

        public WebLoader()
        {
            AngleSharpConfiguration = Configuration.Default.WithDefaultLoader();
            AngleSharpContext = BrowsingContext.New(AngleSharpConfiguration);
        }

        public virtual async Task<IDocument> LoadWebpage(string url, int retries = 10)
        {
            IDocument document;
            int counter = 0;
            do
            {
                document = await AngleSharpContext.OpenAsync(url);
                counter++;
            } while (document == null || (document?.Body.ChildElementCount == 0 && counter < retries));

            return document;
        }

    }

    public class Database
    {
        static string dbFile = @"C:\Users\Thorsten\Desktop\Ö3_Playlist_Review.db";

        public static IList<SongEntry> GetSongs()
        {
            IList<SongEntry> songs;
            using (var db = new LiteDatabase(dbFile))
            {
                songs = db.GetCollection<SongEntry>("Entries").FindAll().ToList<SongEntry>();
            }

            return songs;
        }

        public static void Save(IList<SongEntry> entries)
        {
            using (var db = new LiteDatabase(dbFile))
            {
                foreach(var e in entries)
                db.GetCollection<SongEntry>("Entries").Upsert(e);
            }
        }
        public static void Save(SongEntry entry)
        {
            using (var db = new LiteDatabase(dbFile))
            {
                db.GetCollection<SongEntry>("Entries").Upsert(entry);
            }
        }
        public static void Insert(SongEntry entry)
        {
            using (var db = new LiteDatabase(dbFile))
            {
                db.GetCollection<SongEntry>("Entries").Insert(entry);
            }
        }

    }

    public partial class ViewModel : ObservableObject
    {
        string dbFile = @"C:\Users\Thorsten\Desktop\Ö3_Playlist_Review.db";

        [ObservableProperty]
        IList<SongEntry> entries = new List<SongEntry>();

        [ObservableProperty]
        bool isBusy = false;

        public ViewModel()
        {
            Entries = Database.GetSongs();
        }
        ~ViewModel()
        {
            Database.Save(Entries);
        }

        public async Task Parse()
        {
            IsBusy = true;
            var loader = new WebLoader();

            for (int i = 0; i < 10; i++)
            {
                var doc = await loader.LoadWebpage(@"https://onlineradiobox.com/at/hitradiooe3/playlist/" + i);
                var list = GetSongs(doc);
                foreach (var song in list)
                {
                    if (Entries.FirstOrDefault(x => x.Name.Trim().ToLower() == song.Name.Trim().ToLower()) == null)
                    {
                        Database.Insert(song);
                        Entries.Add(song);
                    }
                }
            }
            IsBusy = false;

            Entries = Database.GetSongs();
        }

        private IList<SongEntry> GetSongs(IDocument doc)
        {
            IList<SongEntry> list = new List<SongEntry>();

            var tmp = doc.Body.GetElementsByClassName("track_history_item");
            foreach (var item in tmp)
            {
                string name = item.Text().Trim();
                var s = new SongEntry() { Name = name };
                list.Add(s);
            }

            return list;
        }

    }


    public partial class MainPage : ContentPage
    {
        public ViewModel vm { get; set; } = new ViewModel();

        public MainPage()
        {
            BindingContext = vm;
            InitializeComponent();
        }

        private async void OnCounterClicked(object sender, EventArgs e)
        {
            await vm.Parse();
        }


    }
}