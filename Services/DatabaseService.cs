using Microsoft.Data.Sqlite;
using System.IO;
using Uttu.Models;

namespace Uttu.Services;

public class DatabaseService
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public DatabaseService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string uttuDir = Path.Combine(localAppData, "Uttu");
        if (!Directory.Exists(uttuDir))
        {
            Directory.CreateDirectory(uttuDir);
        }

        string cacheDir = Path.Combine(uttuDir, "cache");
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }

        _dbPath = Path.Combine(uttuDir, "uttu.db");
        _connectionString = $"Data Source={_dbPath};Cache=Shared;";
    }

    public string DatabasePath => _dbPath;
    public string CachePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uttu", "cache");

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Enable WAL mode for performance and concurrent reads
        using var walCmd = connection.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode=WAL;";
        await walCmd.ExecuteNonQueryAsync();

        // Create Tables
        string schemaSql = """
            CREATE TABLE IF NOT EXISTS posts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                theme TEXT NOT NULL,
                platform TEXT NOT NULL,
                content TEXT NOT NULL,
                status TEXT NOT NULL,
                card_image_path TEXT,
                created_at TEXT NOT NULL,
                scheduled_at TEXT,
                buffer_update_id TEXT
            );

            CREATE TABLE IF NOT EXISTS memories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                text TEXT NOT NULL,
                is_active INTEGER NOT NULL,
                category TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS voice_profiles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                sample_text TEXT NOT NULL,
                avg_sentence_length REAL NOT NULL,
                punctuation_density REAL NOT NULL,
                prohibited_words TEXT NOT NULL,
                hook_style TEXT NOT NULL,
                tone_keywords TEXT NOT NULL,
                analyzed_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
        """;

        using var schemaCmd = connection.CreateCommand();
        schemaCmd.CommandText = schemaSql;
        await schemaCmd.ExecuteNonQueryAsync();

        // Check if memories need seeding
        using var countMemCmd = connection.CreateCommand();
        countMemCmd.CommandText = "SELECT COUNT(*) FROM memories;";
        long memCount = (long)(await countMemCmd.ExecuteScalarAsync() ?? 0);

        if (memCount == 0)
        {
            await SeedInitialMemoriesAsync(connection);
        }

        // Check if posts need seeding
        using var countPostsCmd = connection.CreateCommand();
        countPostsCmd.CommandText = "SELECT COUNT(*) FROM posts;";
        long postsCount = (long)(await countPostsCmd.ExecuteScalarAsync() ?? 0);

        if (postsCount == 0)
        {
            await SeedInitialPostsAsync(connection);
        }

        // Check voice profile
        using var countVoiceCmd = connection.CreateCommand();
        countVoiceCmd.CommandText = "SELECT COUNT(*) FROM voice_profiles;";
        long voiceCount = (long)(await countVoiceCmd.ExecuteScalarAsync() ?? 0);

        if (voiceCount == 0)
        {
            await SeedInitialVoiceProfileAsync(connection);
        }
    }

    private async Task SeedInitialMemoriesAsync(SqliteConnection connection)
    {
        var defaults = new (string text, bool active, string category)[]
        {
            ("Short direct hooks consistently outperform generic questions.", true, "Hooks"),
            ("Pragmatic tone: focus on real software engineering and product realities.", true, "Tone"),
            ("Use emojis at the start of bullet points.", false, "Formatting"),
            ("Vertical double line-spacing for high readability on LinkedIn.", true, "Formatting"),
            ("Never use hollow buzzwords like 'synergy', 'game changer', or 'mindset'.", true, "Rules"),
            ("Highlight real production lessons rather than abstract theory.", true, "Tone"),
            ("The first two lines determine 80% of reach on X and LinkedIn.", true, "Hooks")
        };

        foreach (var item in defaults)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO memories (text, is_active, category, created_at) VALUES (@text, @active, @cat, @created);";
            cmd.Parameters.AddWithValue("@text", item.text);
            cmd.Parameters.AddWithValue("@active", item.active ? 1 : 0);
            cmd.Parameters.AddWithValue("@cat", item.category);
            cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task SeedInitialPostsAsync(SqliteConnection connection)
    {
        var post1 = (
            theme: "Fewer features and more clarity",
            platform: "X",
            content: "A product does not need more features to be better.\n\nOften, the best next step is removing doubt and making a decision clearer for the user.",
            status: "Draft"
        );

        var post2 = (
            theme: "Pruning complexity in the backlog",
            platform: "LinkedIn",
            content: "Not every improvement begins with a new feature in the backlog.\n\nLast week we decided to prune three screens from our onboarding flow.\n\nThe result? Fewer support tickets and higher user retention.\n\nLess is clarity.",
            status: "Draft"
        );

        var posts = new[] { post1, post2 };
        foreach (var p in posts)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO posts (theme, platform, content, status, created_at) VALUES (@theme, @platform, @content, @status, @created);";
            cmd.Parameters.AddWithValue("@theme", p.theme);
            cmd.Parameters.AddWithValue("@platform", p.platform);
            cmd.Parameters.AddWithValue("@content", p.content);
            cmd.Parameters.AddWithValue("@status", p.status);
            cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task SeedInitialVoiceProfileAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO voice_profiles (sample_text, avg_sentence_length, punctuation_density, prohibited_words, hook_style, tone_keywords, analyzed_at)
            VALUES (@sample, @avgLen, @punc, @prohibited, @hook, @tone, @analyzed);
        """;
        cmd.Parameters.AddWithValue("@sample", "Less code means less debt. In software engineering, simplicity is the pinnacle of design.");
        cmd.Parameters.AddWithValue("@avgLen", 11.8);
        cmd.Parameters.AddWithValue("@punc", 6.5);
        cmd.Parameters.AddWithValue("@prohibited", "synergy, disruptive, revolutionary, game changer, leverage, mindset");
        cmd.Parameters.AddWithValue("@hook", "Contrarian / Provocative (Short direct hook)");
        cmd.Parameters.AddWithValue("@tone", "Pragmatic, Direct, Engineering-focused, Zero fluff");
        cmd.Parameters.AddWithValue("@analyzed", DateTime.Now.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    #region Posts Operations

    public async Task<List<PostDraft>> GetDraftPostsAsync()
    {
        var list = new List<PostDraft>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, theme, platform, content, status, card_image_path, created_at, scheduled_at, buffer_update_id FROM posts WHERE status = 'Draft' ORDER BY id DESC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PostDraft
            {
                Id = reader.GetInt32(0),
                Theme = reader.GetString(1),
                Platform = reader.GetString(2),
                Content = reader.GetString(3),
                Status = reader.GetString(4),
                CardImagePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = DateTime.TryParse(reader.GetString(6), out var cDate) ? cDate : DateTime.Now,
                ScheduledAt = reader.IsDBNull(7) ? null : (DateTime.TryParse(reader.GetString(7), out var sDate) ? sDate : null),
                BufferUpdateId = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return list;
    }

    public async Task<List<PostDraft>> GetAllPostsAsync()
    {
        var list = new List<PostDraft>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, theme, platform, content, status, card_image_path, created_at, scheduled_at, buffer_update_id FROM posts ORDER BY id DESC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PostDraft
            {
                Id = reader.GetInt32(0),
                Theme = reader.GetString(1),
                Platform = reader.GetString(2),
                Content = reader.GetString(3),
                Status = reader.GetString(4),
                CardImagePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = DateTime.TryParse(reader.GetString(6), out var cDate) ? cDate : DateTime.Now,
                ScheduledAt = reader.IsDBNull(7) ? null : (DateTime.TryParse(reader.GetString(7), out var sDate) ? sDate : null),
                BufferUpdateId = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return list;
    }

    public async Task<int> SavePostAsync(PostDraft post)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        if (post.Id == 0)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO posts (theme, platform, content, status, card_image_path, created_at, scheduled_at, buffer_update_id)
                VALUES (@theme, @platform, @content, @status, @card, @created, @scheduled, @bufId);
                SELECT last_insert_rowid();
            """;
            cmd.Parameters.AddWithValue("@theme", post.Theme);
            cmd.Parameters.AddWithValue("@platform", post.Platform);
            cmd.Parameters.AddWithValue("@content", post.Content);
            cmd.Parameters.AddWithValue("@status", post.Status);
            cmd.Parameters.AddWithValue("@card", (object?)post.CardImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", post.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@scheduled", post.ScheduledAt.HasValue ? post.ScheduledAt.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@bufId", (object?)post.BufferUpdateId ?? DBNull.Value);

            long newId = (long)(await cmd.ExecuteScalarAsync() ?? 0);
            post.Id = (int)newId;
            return post.Id;
        }
        else
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                UPDATE posts SET
                    theme = @theme,
                    platform = @platform,
                    content = @content,
                    status = @status,
                    card_image_path = @card,
                    scheduled_at = @scheduled,
                    buffer_update_id = @bufId
                WHERE id = @id;
            """;
            cmd.Parameters.AddWithValue("@id", post.Id);
            cmd.Parameters.AddWithValue("@theme", post.Theme);
            cmd.Parameters.AddWithValue("@platform", post.Platform);
            cmd.Parameters.AddWithValue("@content", post.Content);
            cmd.Parameters.AddWithValue("@status", post.Status);
            cmd.Parameters.AddWithValue("@card", (object?)post.CardImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@scheduled", post.ScheduledAt.HasValue ? post.ScheduledAt.Value.ToString("o") : DBNull.Value);
            cmd.Parameters.AddWithValue("@bufId", (object?)post.BufferUpdateId ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return post.Id;
        }
    }

    public async Task UpdatePostStatusAsync(int id, string status, string? bufferUpdateId = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE posts SET status = @status, buffer_update_id = COALESCE(@bufId, buffer_update_id) WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@bufId", (object?)bufferUpdateId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeletePostAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM posts WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    #endregion

    #region Memories Operations

    public async Task<List<MemoryItem>> GetMemoriesAsync()
    {
        var list = new List<MemoryItem>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, text, is_active, category, created_at FROM memories ORDER BY is_active DESC, id DESC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new MemoryItem
            {
                Id = reader.GetInt32(0),
                Text = reader.GetString(1),
                IsActive = reader.GetInt32(2) == 1,
                Category = reader.GetString(3),
                CreatedAt = DateTime.TryParse(reader.GetString(4), out var dt) ? dt : DateTime.Now
            });
        }

        return list;
    }

    public async Task<List<MemoryItem>> GetActiveMemoriesAsync()
    {
        var all = await GetMemoriesAsync();
        return all.Where(m => m.IsActive).ToList();
    }

    public async Task<int> SaveMemoryAsync(MemoryItem memory)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        if (memory.Id == 0)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO memories (text, is_active, category, created_at)
                VALUES (@text, @active, @cat, @created);
                SELECT last_insert_rowid();
            """;
            cmd.Parameters.AddWithValue("@text", memory.Text);
            cmd.Parameters.AddWithValue("@active", memory.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@cat", memory.Category);
            cmd.Parameters.AddWithValue("@created", memory.CreatedAt.ToString("o"));

            long newId = (long)(await cmd.ExecuteScalarAsync() ?? 0);
            memory.Id = (int)newId;
            return memory.Id;
        }
        else
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE memories SET text = @text, is_active = @active, category = @cat WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", memory.Id);
            cmd.Parameters.AddWithValue("@text", memory.Text);
            cmd.Parameters.AddWithValue("@active", memory.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@cat", memory.Category);
            await cmd.ExecuteNonQueryAsync();
            return memory.Id;
        }
    }

    public async Task ToggleMemoryAsync(int id, bool isActive)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE memories SET is_active = @active WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteMemoryAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM memories WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    #endregion

    #region Voice Profile Operations

    public async Task<VoiceProfile> GetVoiceProfileAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, sample_text, avg_sentence_length, punctuation_density, prohibited_words, hook_style, tone_keywords, analyzed_at FROM voice_profiles ORDER BY id DESC LIMIT 1;";

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new VoiceProfile
            {
                Id = reader.GetInt32(0),
                SampleText = reader.GetString(1),
                AvgSentenceLength = reader.GetDouble(2),
                PunctuationDensity = reader.GetDouble(3),
                ProhibitedWords = reader.GetString(4),
                HookStyle = reader.GetString(5),
                ToneKeywords = reader.GetString(6),
                AnalyzedAt = DateTime.TryParse(reader.GetString(7), out var dt) ? dt : DateTime.Now
            };
        }

        return new VoiceProfile();
    }

    public async Task SaveVoiceProfileAsync(VoiceProfile profile)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO voice_profiles (sample_text, avg_sentence_length, punctuation_density, prohibited_words, hook_style, tone_keywords, analyzed_at)
            VALUES (@sample, @avgLen, @punc, @prohibited, @hook, @tone, @analyzed);
        """;
        cmd.Parameters.AddWithValue("@sample", profile.SampleText);
        cmd.Parameters.AddWithValue("@avgLen", profile.AvgSentenceLength);
        cmd.Parameters.AddWithValue("@punc", profile.PunctuationDensity);
        cmd.Parameters.AddWithValue("@prohibited", profile.ProhibitedWords);
        cmd.Parameters.AddWithValue("@hook", profile.HookStyle);
        cmd.Parameters.AddWithValue("@tone", profile.ToneKeywords);
        cmd.Parameters.AddWithValue("@analyzed", DateTime.Now.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    #endregion

    #region Settings Operations

    public async Task<AppSettings> GetSettingsAsync()
    {
        var settings = new AppSettings();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string key = reader.GetString(0);
            string val = reader.GetString(1);

            switch (key)
            {
                case "notification_time": settings.NotificationTime = val; break;
                case "auto_generate_daily": settings.AutoGenerateDaily = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "buffer_mcp_command": settings.BufferMcpCommand = val; break;
                case "buffer_mcp_args": settings.BufferMcpArgs = val; break;
                case "buffer_access_token_encrypted": settings.BufferAccessTokenEncrypted = val; break;
                case "ai_provider": settings.AiProvider = val; break;
                case "ai_model": settings.AiModel = val; break;
                case "ai_api_key_encrypted": settings.AiApiKeyEncrypted = val; break;
                case "ollama_endpoint": settings.OllamaEndpoint = val; break;
                case "card_theme": settings.CardTheme = val; break;
                case "close_to_tray": settings.CloseToTray = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "default_author_handle": settings.DefaultAuthorHandle = val; break;
                case "default_author_name": settings.DefaultAuthorName = val; break;
            }
        }

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var dict = new Dictionary<string, string>
        {
            ["notification_time"] = settings.NotificationTime,
            ["auto_generate_daily"] = settings.AutoGenerateDaily ? "1" : "0",
            ["buffer_mcp_command"] = settings.BufferMcpCommand,
            ["buffer_mcp_args"] = settings.BufferMcpArgs,
            ["buffer_access_token_encrypted"] = settings.BufferAccessTokenEncrypted,
            ["ai_provider"] = settings.AiProvider,
            ["ai_model"] = settings.AiModel,
            ["ai_api_key_encrypted"] = settings.AiApiKeyEncrypted,
            ["ollama_endpoint"] = settings.OllamaEndpoint,
            ["card_theme"] = settings.CardTheme,
            ["close_to_tray"] = settings.CloseToTray ? "1" : "0",
            ["default_author_handle"] = settings.DefaultAuthorHandle,
            ["default_author_name"] = settings.DefaultAuthorName
        };

        foreach (var kv in dict)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO settings (key, value) VALUES (@key, @val) ON CONFLICT(key) DO UPDATE SET value = @val;";
            cmd.Parameters.AddWithValue("@key", kv.Key);
            cmd.Parameters.AddWithValue("@val", kv.Value ?? string.Empty);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    #endregion
}
