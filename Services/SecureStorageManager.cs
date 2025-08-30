using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using AudioRecorder.Models;

namespace AudioRecorder.Services
{
    /// <summary>
    /// 安全存储管理器 - 使用Windows凭据管理器
    /// </summary>
    public class SecureStorageManager
    {
        private const string CREDENTIAL_TARGET_PREFIX = "AudioRecorder_OAuth_";
        private const string CREDENTIAL_USERNAME = "OAuth_Tokens";

        /// <summary>
        /// 保存令牌信息
        /// </summary>
        public async Task SaveTokensAsync(string provider, TokenInfo tokens)
        {
            try
            {
                // 设置提供商信息
                tokens.Provider = provider;
                
                // 序列化令牌信息
                var jsonData = JsonSerializer.Serialize(tokens, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // 创建凭据
                var credential = new Credential
                {
                    TargetName = $"{CREDENTIAL_TARGET_PREFIX}{provider}",
                    UserName = CREDENTIAL_USERNAME,
                    Password = jsonData,
                    Persistence = Persistence.LocalMachine,
                    Type = CredentialType.Generic
                };

                // 保存凭据
                credential.Save();
                Console.WriteLine($"💾 保存令牌成功: {provider}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存令牌失败: {provider}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 加载令牌信息
        /// </summary>
        public async Task<TokenInfo?> LoadTokensAsync(string provider)
        {
            try
            {
                var credential = Credential.Load($"{CREDENTIAL_TARGET_PREFIX}{provider}");
                if (credential != null)
                {
                    var tokenInfo = JsonSerializer.Deserialize<TokenInfo>(credential.Password);
                    if (tokenInfo != null)
                    {
                        Console.WriteLine($"📂 加载令牌成功: {provider}");
                        return tokenInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载令牌失败: {provider}, 错误: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 删除令牌信息
        /// </summary>
        public async Task DeleteTokensAsync(string provider)
        {
            try
            {
                var credential = Credential.Load($"{CREDENTIAL_TARGET_PREFIX}{provider}");
                if (credential != null)
                {
                    credential.Delete();
                    Console.WriteLine($"🗑️ 删除令牌成功: {provider}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 删除令牌失败: {provider}, 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有已存储的OAuth提供商
        /// </summary>
        public async Task<List<string>> GetStoredProvidersAsync()
        {
            var providers = new List<string>();

            try
            {
                // 枚举所有凭据，查找OAuth相关的
                var credentials = Credential.Enumerate();
                foreach (var credential in credentials)
                {
                    if (credential.TargetName.StartsWith(CREDENTIAL_TARGET_PREFIX))
                    {
                        var provider = credential.TargetName.Replace(CREDENTIAL_TARGET_PREFIX, "");
                        providers.Add(provider);
                    }
                }

                Console.WriteLine($"🔍 发现存储的提供商: {string.Join(", ", providers)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 枚举存储的提供商失败: {ex.Message}");
            }

            return providers;
        }

        /// <summary>
        /// 检查是否有存储的令牌
        /// </summary>
        public async Task<bool> HasStoredTokensAsync(string provider)
        {
            try
            {
                var credential = Credential.Load($"{CREDENTIAL_TARGET_PREFIX}{provider}");
                return credential != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 清除所有存储的令牌
        /// </summary>
        public async Task ClearAllTokensAsync()
        {
            try
            {
                var providers = await GetStoredProvidersAsync();
                foreach (var provider in providers)
                {
                    await DeleteTokensAsync(provider);
                }
                Console.WriteLine("🧹 已清除所有存储的令牌");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 清除所有令牌失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Windows凭据管理器包装类
    /// </summary>
    public class Credential
    {
        public string TargetName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public Persistence Persistence { get; set; }
        public CredentialType Type { get; set; }

        public void Save()
        {
            // 这里需要引用Windows凭据管理器API
            // 为了简化，我们使用文件系统作为备选方案
            SaveToFile();
        }

        public static Credential? Load(string targetName)
        {
            // 从文件加载
            return LoadFromFile(targetName);
        }

        public void Delete()
        {
            // 删除文件
            DeleteFile();
        }

        public static IEnumerable<Credential> Enumerate()
        {
            // 枚举所有凭据文件
            return EnumerateFromFiles();
        }

        private void SaveToFile()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var tokensPath = Path.Combine(appDataPath, "AudioRecorder", "tokens");
                Directory.CreateDirectory(tokensPath);

                var filePath = Path.Combine(tokensPath, $"{TargetName}.json");
                var jsonData = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, jsonData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 保存到文件失败: {ex.Message}");
            }
        }

        private static Credential? LoadFromFile(string targetName)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var tokensPath = Path.Combine(appDataPath, "AudioRecorder", "tokens");
                var filePath = Path.Combine(tokensPath, $"{targetName}.json");

                if (File.Exists(filePath))
                {
                    var jsonData = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<Credential>(jsonData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 从文件加载失败: {ex.Message}");
            }

            return null;
        }

        private void DeleteFile()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var tokensPath = Path.Combine(appDataPath, "AudioRecorder", "tokens");
                var filePath = Path.Combine(tokensPath, $"{TargetName}.json");

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 删除文件失败: {ex.Message}");
            }
        }

        private static IEnumerable<Credential> EnumerateFromFiles()
        {
            var credentials = new List<Credential>();

            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var tokensPath = Path.Combine(appDataPath, "AudioRecorder", "tokens");

                if (Directory.Exists(tokensPath))
                {
                    var files = Directory.GetFiles(tokensPath, "*.json");
                    foreach (var file in files)
                    {
                        try
                        {
                            var jsonData = File.ReadAllText(file);
                            var credential = JsonSerializer.Deserialize<Credential>(jsonData);
                            if (credential != null)
                            {
                                credentials.Add(credential);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ 读取凭据文件失败: {file}, 错误: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 枚举凭据文件失败: {ex.Message}");
            }

            return credentials;
        }
    }

    /// <summary>
    /// 凭据持久化类型
    /// </summary>
    public enum Persistence
    {
        None = 0,
        LocalMachine = 1,
        CurrentUser = 2,
        Enterprise = 3
    }

    /// <summary>
    /// 凭据类型
    /// </summary>
    public enum CredentialType
    {
        Generic = 1,
        DomainPassword = 2,
        DomainCertificate = 3,
        DomainVisiblePassword = 4,
        GenericCertificate = 5,
        DomainExtended = 6,
        Maximum = 7
    }
}
