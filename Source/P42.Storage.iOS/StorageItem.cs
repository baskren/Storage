﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Foundation;

namespace P42.Storage.Native
{
    public class StorageItem
    {
        private static NSData BookmarkForUrl(NSUrl url)
        {
            if (url != null)
            {
                var bookmarksObj = NSUserDefaults.StandardUserDefaults.ValueForKey(new NSString("Bookmarks")) as NSArray;
                var nsBookmarks = bookmarksObj?.MutableCopy() as NSMutableArray ?? new NSMutableArray();
                var bookmarks = new List<NSData>();
                for (uint i = 0; i < nsBookmarks.Count; i++)
                    bookmarks.Add(nsBookmarks.GetItem<NSData>(i));
                foreach (var bookmark in bookmarks)
                {
                    var bookmarkUrl = NSUrl.FromBookmarkData(bookmark,
                        //NSUrlBookmarkResolutionOptions.WithSecurityScope,
                        NSUrlBookmarkResolutionOptions.WithoutUI,
                        null,
                        out bool isStale,
                        out NSError error1
                        );
                    if (bookmarkUrl != null && error1 == null)
                    {
                        if (bookmarkUrl.Path == url.Path)
                            return bookmark;
                    }
                    else
                    {
                        if (error1 != null)
                        {
                            Console.WriteLine("Bookmark error: Bookmark for url [" + url + "] gave error [" + error1.Description + "] when trying to convert to URL.");
                        }
                        nsBookmarks.RemoveObject((nint)nsBookmarks.IndexOf(bookmark));
                        NSUserDefaults.StandardUserDefaults.SetValueForKey(nsBookmarks, new NSString("Bookmarks"));
                    }
                }

                var newBookmark = url.CreateBookmarkData(NSUrlBookmarkCreationOptions.SuitableForBookmarkFile, new string[] { }, null, out NSError error2);
                if (error2 != null)
                {
                    Console.WriteLine("Can not get bookmark for url path [" + url.Path + "].");
                    Console.WriteLine("ERROR: " + error2);
                    return null;
                }
                else
                {
                    nsBookmarks.Add(newBookmark);
                    NSUserDefaults.StandardUserDefaults.SetValueForKey(nsBookmarks, new NSString("Bookmarks"));
                    return newBookmark;
                }
            }
            return null;
        }

        NSData _bookmark;
        internal NSUrl Url
        {
            get
            {
                if (_bookmark == null)
                    return null;
                var url = NSUrl.FromBookmarkData(_bookmark,
                    //NSUrlBookmarkResolutionOptions.WithSecurityScope,
                    NSUrlBookmarkResolutionOptions.WithoutUI,
                    null,
                    out bool isStale,
                    out NSError error
                    );
                if (error != null)
                {
                    Console.WriteLine("Can no longer get url for bookmarked file.");
                    Console.WriteLine("ERROR: " + error);
                }
                if (isStale)
                {
                    Url = url;
                }
                return url;
            }
            set
            {
                _bookmark = BookmarkForUrl(value);
            }
        }

        /// <summary>
        /// Gets the name of the file including the file name extension.
        /// </summary>
        public string Name
        //    => System.IO.Path.GetFileName(Path);
        {
            get
            {
                if (Url is NSUrl url)
                    return url.LastPathComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the full file-system path of the current file, if the file has a path.
        /// </summary>
        public string Path
        {
            get
            {
                if (Url is NSUrl url)
                    return url.Path;
                return null;
            }
        }

        public StorageItem(string path)
        {
            Url = NSUrl.CreateFileUrl(path, null);
            if (Url == null)
                throw new ArgumentException("Cannot initialize Native.StorageItem for path [" + path + "]");
        }


        public StorageItem(NSUrl url)
        {
            Url = url;
            if (Url == null)
                throw new ArgumentException("Cannot initialize Native.StorageItem for URL [" + url + "]");
        }



        /// <summary>
        /// Determines whether the current <see cref="StorageFile"/> matches the specified <see cref="StorageItemTypes"/> value.
        /// </summary>
        /// <param name="type">The value to match against.</param>
        /// <returns>True if the <see cref="StorageFile"/> matches the specified value; otherwise false.</returns>
        /// <seealso cref="StorageItemTypes"/>
        public bool IsOfType(StorageItemTypes type)
            => type == StorageItemTypes.File;

        /// <summary>
        /// Gets the attributes of a file.
        /// </summary>
        public FileAttributes Attributes
            => FileAttributesHelper.FromIOFileAttributes(File.GetAttributes(Path));

        /// <summary>
        /// Gets the date and time when the current file was created. 
        /// </summary>
        public DateTimeOffset DateCreated
        {
            get
            {
                var utc = File.GetCreationTimeUtc(Path);
                var local = File.GetCreationTime(Path);
                var offset = local - utc;
                return new DateTimeOffset(local, offset);
            }
        }

        /// <summary>
        /// Gets the timestamp of the last time the file was modified.
        /// </summary>
        public DateTimeOffset DateModified
        {
            get
            {
                DateTime time;
                TimeSpan offset;
                time = File.GetLastWriteTime(Path);
                offset = time.Subtract(File.GetLastWriteTimeUtc(Path));
                return new DateTimeOffset(time, offset);
            }
        }

        /// <summary>
        /// Gets the size of the file in bytes.
        /// </summary>
        public ulong Size
        {
            get
            {
                FileInfo fi = new FileInfo(Path);
                return (ulong)fi.Length;
            }
        }



        /// <summary>
        /// Indicates whether the current file is equal to the specified file.
        /// </summary>
        /// <param name="item">The <see cref="IStorageItem"/>  object that represents a file to compare against.</param>
        /// <returns>Returns true if the current file is equal to the specified file; otherwise false.</returns>
        public bool IsEqual(IStorageItem item)
            => Path == item.Path;

        /// <summary>
        /// Gets the parent folder of the current file.
        /// </summary>
        /// <returns></returns>
        public Task<IStorageFolder> GetParentAsync()
        {
            return Task.Run<IStorageFolder>(() =>
            {
                var parent = Directory.GetParent(Path);
                return parent == null ? null : new StorageFolder(parent.FullName);
            });
        }

        /// <summary>
        /// Deletes the current file.
        /// </summary>
        /// <returns></returns>
        public Task DeleteAsync()
            => DeleteAsync(StorageDeleteOption.Default);


        /// <summary>
        /// Deletes the current file, optionally deleting the item permanently.
        /// </summary>
        /// <returns></returns>
        public Task DeleteAsync(StorageDeleteOption option)
            => Task.Run(() =>
            {
                if (Url is NSUrl url)
                {
                    url.StartAccessingSecurityScopedResource();
                    if (option == StorageDeleteOption.Default)
                    {
                        if (NSFileManager.DefaultManager.TrashItem(url, out NSUrl resultingUrl, out NSError error))
                            Url = resultingUrl;
                        else
                        {
                            Console.WriteLine("Cannot delete file [" + url.Path + "].");
                            Console.WriteLine("ERROR: " + error);
                        }
                    }
                    else if (!NSFileManager.DefaultManager.Remove(url, out NSError error))
                    {
                        Console.WriteLine("Cannot delete file [" + url.Path + "].");
                        Console.WriteLine("ERROR: " + error);
                        Url = null;
                    }
                    url.StopAccessingSecurityScopedResource();
                }
                return Task.CompletedTask;
            });

    }
}