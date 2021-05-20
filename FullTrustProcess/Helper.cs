﻿using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;
using Windows.Storage.Streams;

namespace FullTrustProcess
{
    public static class Helper
    {
        public static IEnumerable<FileInfo> GetAllSubFiles(DirectoryInfo Directory)
        {
            foreach (FileInfo File in Directory.EnumerateFiles())
            {
                yield return File;
            }

            foreach (DirectoryInfo Dic in Directory.EnumerateDirectories())
            {
                foreach (FileInfo SubFile in GetAllSubFiles(Dic))
                {
                    yield return SubFile;
                }
            }
        }

        public static void ExecuteOnSTAThread(Action Executor)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Exception Ex = null;

                Thread STAThread = new Thread(() =>
                {
                    Ole32.OleInitialize();

                    try
                    {
                        Executor();
                    }
                    catch (Exception ex)
                    {
                        Ex = ex;
                    }
                    finally
                    {
                        Ole32.OleUninitialize();
                    }
                })
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };
                STAThread.SetApartmentState(ApartmentState.STA);
                STAThread.Start();
                STAThread.Join();

                if (Ex != null)
                {
                    throw Ex;
                }
            }
            else
            {
                Executor();
            }
        }

        public static T ExecuteOnSTAThread<T>(Func<T> Executor)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                T Result = default;
                Exception Ex = null;

                Thread STAThread = new Thread(() =>
                {
                    Ole32.OleInitialize();

                    try
                    {
                        Result = Executor();
                    }
                    catch (Exception ex)
                    {
                        Ex = ex;
                    }
                    finally
                    {
                        Ole32.OleUninitialize();
                    }
                })
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };
                STAThread.SetApartmentState(ApartmentState.STA);
                STAThread.Start();
                STAThread.Join();

                if (Ex == null)
                {
                    return Result;
                }
                else
                {
                    throw Ex;
                }
            }
            else
            {
                return Executor();
            }
        }

        public static string GetPackageFamilyNameFromUWPShellLink(string LinkPath)
        {
            using (ShellItem LinkItem = new ShellItem(LinkPath))
            {
                return LinkItem.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Link.TargetParsingPath).Split('!').FirstOrDefault();
            }
        }

        public static async Task<byte[]> GetIconDataFromPackageFamilyName(string PackageFamilyName)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                using (IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync())
                using (DataReader Reader = new DataReader(IconStream))
                {
                    byte[] Buffer = new byte[IconStream.Size];

                    await Reader.LoadAsync(Convert.ToUInt32(IconStream.Size));

                    Reader.ReadBytes(Buffer);

                    return Buffer;
                }
            }
            else
            {
                return Array.Empty<byte>();
            }
        }

        public static bool CheckIfPackageFamilyNameExist(string PackageFamilyName)
        {
            PackageManager Manager = new PackageManager();

            return Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).Any();
        }

        public static bool LaunchApplicationFromAUMID(string AppUserModelId, params string[] PathArray)
        {
            if (PathArray.Length > 0)
            {
                return ExecuteOnSTAThread(() =>
                {
                    List<ShellItem> SItemList = new List<ShellItem>(PathArray.Length);

                    try
                    {
                        if (new Shell32.ApplicationActivationManager() is Shell32.IApplicationActivationManager Manager)
                        {
                            foreach (string Path in PathArray)
                            {
                                SItemList.Add(new ShellItem(Path));
                            }

                            using (ShellItemArray ItemArray = new ShellItemArray(SItemList))
                            {
                                Manager.ActivateForFile(AppUserModelId, ItemArray.IShellItemArray, "Open", out _);
                            }

                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                    finally
                    {
                        SItemList.ForEach((Item) => Item.Dispose());
                    }
                });
            }
            else
            {
                return LaunchApplicationFromAUMID(AppUserModelId);
            }
        }

        public static bool LaunchApplicationFromAUMID(string AppUserModelId)
        {
            return ExecuteOnSTAThread(() =>
            {
                try
                {
                    if (new Shell32.ApplicationActivationManager() is Shell32.IApplicationActivationManager Manager)
                    {
                        Manager.ActivateApplication(AppUserModelId, null, Shell32.ACTIVATEOPTIONS.AO_NONE, out _);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<bool> LaunchApplicationFromPackageFamilyName(string PackageFamilyName, params string[] PathArray)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                foreach (AppListEntry Entry in await Pack.GetAppListEntriesAsync())
                {
                    if (PathArray.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(Entry.AppUserModelId))
                        {
                            return LaunchApplicationFromAUMID(Entry.AppUserModelId, PathArray);
                        }
                    }
                    else
                    {
                        if (await Entry.LaunchAsync())
                        {
                            return true;
                        }
                        else if (!string.IsNullOrEmpty(Entry.AppUserModelId))
                        {
                            return LaunchApplicationFromAUMID(Entry.AppUserModelId);
                        }
                    }
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        public static async Task<InstalledApplicationPackage> GetInstalledApplicationAsync(string PackageFamilyName)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                try
                {
                    RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                    using (IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync())
                    using (DataReader Reader = new DataReader(IconStream))
                    {
                        await Reader.LoadAsync(Convert.ToUInt32(IconStream.Size));

                        byte[] Logo = new byte[IconStream.Size];

                        Reader.ReadBytes(Logo);

                        return new InstalledApplicationPackage(Pack.DisplayName, Pack.PublisherDisplayName, Pack.Id.FamilyName, Logo);
                    }
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public static async Task<InstalledApplicationPackage[]> GetInstalledApplicationAsync()
        {
            List<InstalledApplicationPackage> Result = new List<InstalledApplicationPackage>();

            PackageManager Manager = new PackageManager();

            foreach (Package Pack in Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageTypes.Main).Where((Pack) => !Pack.DisplayName.Equals("XSurfUwp", StringComparison.OrdinalIgnoreCase) && !Pack.IsDevelopmentMode).OrderBy((Pack) => Pack.Id.Publisher))
            {
                try
                {
                    RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                    using (IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync())
                    using (DataReader Reader = new DataReader(IconStream))
                    {
                        await Reader.LoadAsync(Convert.ToUInt32(IconStream.Size));

                        byte[] Logo = new byte[IconStream.Size];

                        Reader.ReadBytes(Logo);

                        Result.Add(new InstalledApplicationPackage(Pack.DisplayName, Pack.PublisherDisplayName, Pack.Id.FamilyName, Logo));
                    }
                }
                catch
                {
                    continue;
                }
            }

            return Result.ToArray();
        }
    }
}
