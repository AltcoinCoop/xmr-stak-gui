﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming
namespace XmrStakGui
{
    public class Config
    {
        #region json properties

        [JsonProperty("settings")]
        public Settings Settings { get; set; }

        [JsonProperty("miners")]
        public List<Miner> Miners { get; set; }

        [JsonProperty("configurations")]
        public List<Configuration> Configurations { get; set; }

        #endregion

        #region methods

        public void Save()
        {
            File.WriteAllText(GetConfigFilePath(), JsonConvert.SerializeObject(this, Formatting.Indented),
                Encoding.UTF8);
        }

        public Configuration Import(string minerFile)
        {
            ImportMiner(minerFile);
            var configuration = ImportConfiguration(minerFile);
            Save();

            return configuration;
        }

        public void ImportMiner(string minerFile)
        {
            var fileName = Path.GetFileName(minerFile)?.ToLower();
            if (fileName == null) return;
            var miner = Miners.FirstOrDefault(m => m.Path.Equals(minerFile, StringComparison.InvariantCultureIgnoreCase));
            if (miner != null) return;
            miner = new Miner
            {
                Path = minerFile
            };
            Miners.Add(miner);
        }

        public Configuration ImportConfiguration(string minerFile)
        {
            var fileName = Path.GetFileName(minerFile)?.ToLower();
            var path = Path.GetDirectoryName(minerFile);
            if (path == null || fileName == null) return null;
            var configTxtPath = Path.Combine(path, Consts.ConfigTxtFile);
            if (!File.Exists(configTxtPath)) return null;
            var json = $"{{{File.ReadAllText(configTxtPath)}}}";

            var configuration = new Configuration();

            switch (fileName)
            {
                case Consts.CpuMiner:
                    configuration.Cpu = JsonConvert.DeserializeObject<XmrStakCpu>(json);
                    break;
                case Consts.AmdMiner:
                    configuration.Amd = JsonConvert.DeserializeObject<XmrStakAmd>(json);
                    break;
                case Consts.NvidiaMiner:
                    configuration.Nvidia = JsonConvert.DeserializeObject<XmrStakNvidia>(json);
                    break;
                default:
                    return null;
            }

            var existingConfiguration = Configurations.FirstOrDefault(c => EqualConfigurations(c, configuration));
            if (existingConfiguration != null)
                return existingConfiguration;

            configuration.Name = $"{fileName} {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            Configurations.Add(configuration);
            return configuration;
        }

        #endregion

        #region static methods


        private static string GetConfigFilePath()
        {
            return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty, Consts.ConfigFile);
        }

        public static Config Load()
        {
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(GetConfigFilePath()));
        }

        public static void SaveTxt<T>(Configuration config, string minerFile)
        {
            var path = Path.GetDirectoryName(minerFile);
            if (path == null) return;
            var configTxtPath = Path.Combine(path, Consts.ConfigTxtFile);
            var property = config.GetType()
                .GetProperties()
                .FirstOrDefault(p => p.PropertyType == typeof(T));

            if (property == null) return;

            var json = JsonConvert.SerializeObject(property.GetValue(config), Formatting.Indented);
            var text = json.Trim('{', '}', ' ', '\n', '\r', '\t');

            File.WriteAllText(configTxtPath, text, Encoding.UTF8);
        }

        private static bool EqualConfigurations(Configuration configuration1, Configuration configuration2)
        {
            return MemberCompare(configuration1.Cpu, configuration2.Cpu)
                   && MemberCompare(configuration1.Amd, configuration2.Amd)
                   && MemberCompare(configuration1.Nvidia, configuration2.Nvidia);
        }

        private static bool MemberCompare<T>(T left, T right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            var type = left.GetType();
            if (type != right.GetType())
                return false;

            if (left is ValueType)
                return left.Equals(right);

            // all Arrays, Lists, IEnumerable<> etc implement IEnumerable
            if (left is IEnumerable)
            {
                var enumerable = right as IEnumerable;
                if (enumerable == null) return true;
                var rightEnumerator = enumerable.GetEnumerator();
                rightEnumerator.Reset();
                foreach (var leftItem in left as IEnumerable)
                    // unequal amount of items
                    if (!rightEnumerator.MoveNext())
                    {
                        return false;
                    }
                    else
                    {
                        if (!MemberCompare(leftItem, rightEnumerator.Current))
                            return false;
                    }
            }
            else
            {
                // compare each property
                foreach (var info in type.GetProperties(
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Instance |
                        BindingFlags.GetProperty))
                    // TODO: need to special-case indexable properties
                    if (!MemberCompare(info.GetValue(left, null), info.GetValue(right, null)))
                        return false;

                // compare each field
                foreach (var info in type.GetFields(
                    BindingFlags.GetField |
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.Instance))
                    if (!MemberCompare(info.GetValue(left), info.GetValue(right)))
                        return false;
            }
            return true;
        }

        #endregion
    }
}