using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.Collections.ObjectModel;

namespace Scanner.Models
{
    public partial class Project : ObservableObject
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        [ObservableProperty]
        private bool isSaving;

        [ObservableProperty]
        private bool isSaved;

        public ObservableCollection<IProjectPage> Pages
        {
            get;
            private set;
        }

        public TargetFormat Format;

        public bool IsPdf => Format == TargetFormat.PDF;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private Project(IList<IProjectPage> pages, TargetFormat format)
        {
            Pages = new ObservableCollection<IProjectPage>(pages);
            Format = format;
        }

        public static async Task<Project> CreateAsync(IList<StorageFile> files, TargetFormat format)
        {
            // empty folder
            await AppDataService.EmptyFolderAsync(AppDataService.ProjectFolder);

            // create pages
            List<IProjectPage> pages = new();
            for (int i = 0; i < files.Count; i++)
            {
                pages.Add(await CreatePageFromFileAsync(files[i], i));
            }

            // create project
            return new Project(pages, format);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<List<IProjectPage>> AddFilesAsync(Dictionary<StorageFile, int> insertions)
        {
            // keep track of changes in case of error
            List<StorageFile> copiedFiles = new();
            List<IProjectPage> insertedPages = new();

            try
            {
                // add files
                foreach (KeyValuePair<StorageFile, int> insertion in insertions)
                {
                    IProjectPage page = await CreatePageFromFileAsync(insertion.Key, insertion.Value);
                    copiedFiles.Add(page.SourceFile);

                    Pages.Insert(insertion.Value, page);
                    insertedPages.Add(page);
                }

            }
            catch (Exception exc)
            {
                // roll back changes
                foreach (StorageFile file in copiedFiles)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }

                foreach (IProjectPage page in insertedPages)
                {
                    Pages.Remove(page);
                }

                throw new ProjectException(exc);
            }

            // update indices
            for (int i = 0; i < Pages.Count; i++)
            {
                Pages[i].Index = i;
            }

            return insertedPages;
        }

        public async Task RemovePagesAsync(List<IProjectPage> pages, bool isUndoing)
        {
            // keep track of changes in case of error
            List<StorageFile> deletedFiles = new();
            List<int> deletedIndices = new();

            try
            {
                // remove pages
                foreach (IProjectPage page in pages)
                {
                    deletedFiles.Add(page.SourceFile);
                    deletedIndices.Add(page.Index);

                    if (page is ImagePage)
                    {
                        if (isUndoing)
                        {
                            // move to redo folder
                            await page.SourceFile.MoveAsync(AppDataService.RedoFolder, page.SourceFile.Name, NameCollisionOption.GenerateUniqueName);
                        }
                        else
                        {
                            // move to undo folder
                            await page.SourceFile.MoveAsync(AppDataService.UndoFolder, page.SourceFile.Name, NameCollisionOption.GenerateUniqueName);
                        }
                    }

                    Pages.Remove(page);
                }
            }
            catch (Exception exc)
            {
                // roll back changes
                foreach (StorageFile file in deletedFiles)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                for (int i = 0; i < deletedIndices.Count; i++)
                {
                    Pages.Insert(deletedIndices[i], pages[i]);
                }
                throw new ProjectException(exc);
            }

            // update indices
            for (int i = 0; i < Pages.Count; i++)
            {
                Pages[i].Index = i;
            }

            IsSaved = false;
        }

        private static async Task<IProjectPage> CreatePageFromFileAsync(StorageFile file, int index)
        {
            if (file == null) throw new ArgumentException("Can't create IProjectPage from null file");

            switch (file.FileType.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".tif":
                case ".tiff":
                    return await ImagePage.CreateAsync(file, index);
                case ".pdf":
                    throw new NotImplementedException();
                default:
                    throw new ArgumentException("Failed to create IProjectPage due to incompatible file format");
            }
        }
    }
}
