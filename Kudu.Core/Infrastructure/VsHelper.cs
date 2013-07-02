﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Kudu.Core.SourceControl;

namespace Kudu.Core.Infrastructure
{
    internal static class VsHelper
    {
        private static readonly Guid _wapGuid = new Guid("349c5851-65df-11da-9384-00065b846f21");

        public static IList<VsSolution> GetSolutions(string path, IFileFinder fileFinder, SearchOption searchOption = SearchOption.AllDirectories)
        {
            IEnumerable<string> filesList = fileFinder.ListFiles(path, searchOption, "*.sln");
            return filesList.Select(s => new VsSolution(s)).ToList();
        }

        /// <summary>
        /// Locates the solution(s) where the specified project is (search up the tree up to the repository path)
        /// </summary>
        public static IList<VsSolution> FindContainingSolutions(string repositoryPath, string targetPath, IFileFinder fileFinder)
        {
            string solutionsPath = PathUtility.CleanPath(targetPath);
            repositoryPath = PathUtility.CleanPath(repositoryPath);

            while (solutionsPath != null && solutionsPath.Contains(repositoryPath))
            {
                var solutionsFound = from solution in GetSolutions(solutionsPath, fileFinder, SearchOption.TopDirectoryOnly)
                                     where ExistsInSolution(solution, targetPath)
                                     select solution;

                if (solutionsFound.Any())
                {
                    return solutionsFound.ToList();
                }

                if (PathUtility.PathsEquals(solutionsPath, repositoryPath))
                {
                    break;
                }

                var parent = Directory.GetParent(solutionsPath);
                solutionsPath = parent != null ? parent.ToString() : null;
            }

            return new List<VsSolution>();
        }

        /// <summary>
        /// Locates the unambiguous solution matching this project
        /// </summary>
        public static VsSolution FindContainingSolution(string repositoryPath, string targetPath, IFileFinder fileFinder)
        {
            var solutions = FindContainingSolutions(repositoryPath, targetPath, fileFinder);

            // Don't want to use SingleOrDefault since that throws
            if (solutions.Count == 0 || solutions.Count > 1)
            {
                return null;
            }

            return solutions[0];
        }

        public static bool IsWap(string projectPath)
        {
            return IsWap(GetProjectTypeGuids(projectPath));
        }

        public static bool IsWap(IEnumerable<Guid> projectTypeGuids)
        {
            return projectTypeGuids.Contains(_wapGuid);
        }

        public static IEnumerable<Guid> GetProjectTypeGuids(string path)
        {
            var document = XDocument.Parse(File.ReadAllText(path));

            var guids = from propertyGroup in document.Root.Elements(GetName("PropertyGroup"))
                        let projectTypeGuids = propertyGroup.Element(GetName("ProjectTypeGuids"))
                        where projectTypeGuids != null
                        from guid in projectTypeGuids.Value.Split(';')
                        select new Guid(guid.Trim('{', '}'));
            return guids;
        }

        private static bool ExistsInSolution(VsSolution solution, string targetPath)
        {
            return (from p in solution.Projects
                    where PathUtility.PathsEquals(p.AbsolutePath, targetPath)
                    select p).Any();
        }

        private static XName GetName(string name)
        {
            return XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");
        }
    }
}
