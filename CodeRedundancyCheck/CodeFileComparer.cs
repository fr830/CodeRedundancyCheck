﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeRedundancyCheck.Extensions;
using CodeRedundancyCheck.Interface;
using CodeRedundancyCheck.Model;

namespace CodeRedundancyCheck
{
    public class CodeFileComparer
    {
        public List<ICodeLineFilter> CodeLineFilters { get; private set; } = new List<ICodeLineFilter>();

        public List<CodeMatch> GetMatches(int minimumMatchingLines, CodeFile firstCodeFile, params CodeFile[] codeFiles)
        {
            var files = new List<CodeFile>(codeFiles)
            {
                firstCodeFile
            };

            return this.GetMatches(minimumMatchingLines, files);
        }

        public List<CodeMatch> GetMatches(int minimumMatchingLines, IEnumerable<CodeFile> codeFiles)
        {
            var codeFileList = codeFiles as IList<CodeFile> ?? codeFiles.ToList();

            var comparables = new HashSet<CodeFile>(codeFileList);

            if (comparables.Count < 1)
            {
                throw new Exception("At least one files must be matched (with itself)");
            }

            var codeFileQueue = new Queue<CodeFile>(codeFileList);

            var result = new Dictionary<string, CodeMatch>(50000);

            var sourceLines = new List<CodeLine>(10000);
            var compareLines = new List<CodeLine>(10000);

            var addedBlocks = new HashSet<string>();

            while (codeFileQueue.Count > 0)
            {
                var sourceFile = codeFileQueue.Dequeue();

                var allSourceLines = sourceFile.CodeLines;

                foreach (var compareFile in comparables)
                {
                    for (var sourceFileLineIndex = 0; sourceFileLineIndex < allSourceLines.Count; sourceFileLineIndex++)
                    {
                        var sourceLine = allSourceLines[sourceFileLineIndex];

                        List<CodeLine> lineMatches;

                        if (compareFile.CodeLinesDictionary.TryGetValue(sourceLine.WashedLineText, out lineMatches) == false)
                        {
                            continue;
                        }

                        if (this.MayStartBlock(sourceLine, sourceFile) == false)
                        {
                            continue;
                        }

                        foreach (var lineMatch in lineMatches)
                        {
                            if (compareFile == sourceFile)
                            {
                                if (lineMatch.CodeFileLineIndex <= sourceFileLineIndex)
                                {
                                    continue;
                                }
                            }

                            var sourceFileLineIndexTemp = sourceFileLineIndex;
                            sourceLine = allSourceLines[sourceFileLineIndex];

                            // If comparing to ourself, start checking one line below the current one
                            int compareFileLineIndex = lineMatch.CodeFileLineIndex;

                            // Only compare to forward to the match we found or circular findings would occur
                            var lastSourceLine = compareFile == sourceFile ? lineMatch.CodeFileLineIndex : allSourceLines.Count;


                            int matchingLineCount = 0;

                            var compareFileLineIndexTemp = compareFileLineIndex;
                            var compareLine = compareFile.CodeLines[compareFileLineIndexTemp];

                            sourceLines.Clear();
                            compareLines.Clear();

                            //                            while (sourceLine.HashCode == compareLine.HashCode && string.Compare(sourceLine.WashedLine, compareLine.WashedLine, StringComparison.OrdinalIgnoreCase) == 0)
                            while (string.Compare(sourceLine.WashedLineText, compareLine.WashedLineText, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                sourceLines.Add(sourceLine);
                                compareLines.Add(compareLine);

                                matchingLineCount++;

                                sourceFileLineIndexTemp++;
                                compareFileLineIndexTemp++;

                                if (sourceFileLineIndexTemp >= lastSourceLine || compareFileLineIndexTemp >= compareFile.CodeLines.Count)
                                {
                                    break;
                                }

                                sourceLine = allSourceLines[sourceFileLineIndexTemp];
                                compareLine = compareFile.CodeLines[compareFileLineIndexTemp];
                            }

                            if (matchingLineCount >= minimumMatchingLines)
                            {
                                var compareLinesKey = GetBlockKey(compareFile.Filename, compareLines, matchingLineCount);
                                var sourceLinesKey = GetBlockKey(sourceFile.Filename, sourceLines, matchingLineCount);

                                if (addedBlocks.DoesNotContainAll(compareLinesKey, sourceLinesKey))
                                {
                                    addedBlocks.AddMultiple(sourceLinesKey, compareLinesKey);

                                    if (sourceLines[0].WashedLineText == "Dim Cause As Integer")
                                    {
                                        var xaxa = 1;
                                    }

                                    //// Remove target blocks that are part of the added block
                                    for (int i = 1; i < matchingLineCount; i++)
                                    {
                                        var compareLinesKeyTemp = GetBlockKey(compareFile.Filename, compareLines[0].CodeFileLineIndex + i, matchingLineCount - i);
                                        var sourceLinesKeyTemp = GetBlockKey(sourceFile.Filename, sourceLines[0].CodeFileLineIndex + i, matchingLineCount - i);
                                        addedBlocks.AddMultiple(compareLinesKeyTemp, sourceLinesKeyTemp);
                                    }

                                    CodeMatch match;
                                    if (result.TryGetValue(sourceLinesKey, out match) == false)
                                    {
                                        match = new CodeMatch
                                        {
                                            CodeFileMatches = new List<CodeFileMatch>(),
                                            Lines = matchingLineCount
                                        };

                                        match.CodeFileMatches.Add(new CodeFileMatch(sourceFile, sourceLines[0].CodeFileLineIndex, new List<CodeLine>(sourceLines)));
                                        result.Add(sourceLinesKey, match);
                                    }

                                    match.CodeFileMatches.Add(new CodeFileMatch(compareFile, compareLines[0].CodeFileLineIndex, new List<CodeLine>(compareLines)));
                                }

                            }
                        }
                    }
                }
            }

            return result.Values.ToList();
        }

        private static string GetBlockKey(string filename, List<CodeLine> codeLines, int matchingLineCount)
        {
            return GetBlockKey(filename, codeLines[0], matchingLineCount);
        }

        private static string GetBlockKey(string filename, CodeLine codeLine, int matchingLineCount)
        {
            return GetBlockKey(filename, codeLine.CodeFileLineIndex, matchingLineCount);
        }

        private static string GetBlockKey(string filename, int codeFileLineIndex, int matchingLineCount)
        {
            return filename + "_" + codeFileLineIndex + "_" + matchingLineCount;
        }

        private bool MayStartBlock(CodeLine sourceLine, CodeFile sourceFile)
        {
            foreach (var filter in this.CodeLineFilters)
            {
                var result = filter.MayStartBlock(sourceLine, sourceFile);

                if (result == false)
                    return false;
            }

            return true;
        }


        public IEnumerable<CodeLine> LoadFileData(string filename)
        {
            return File.ReadAllLines(filename).Select((text, lineNumber) => 
            new CodeLine(
                originalLineText: text, 
                originalLineNumber: lineNumber + 1, 
                originalLinePosition: 0));
        }


    }
}
