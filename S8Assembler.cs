﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace S8Debugger
{
    // https://github.com/PSTNorge/slede8/blob/main/src/assembler.ts
    public class S8Assembler
    {
        public static readonly UInt16 UNDEFINED = 0XFFFF;

        class LabelDefinition
        {
            public String Label;
            public UInt16 Address;
        }

        public class Labels
        {
            List<LabelDefinition> _labels = new List<LabelDefinition>();

            internal UInt16 mapLabelToAddress(string label)
            {
                foreach (LabelDefinition l in _labels)
                {
                    if (l.Label == label)
                    {
                        return l.Address;
                    }
                }
                return (UInt16)UNDEFINED;
            }
            internal void addLabelAndAddress(string label, UInt16 address)
            {
                _labels.Add(new LabelDefinition() { Label = label, Address = address });
            }
        };

        public class InstructionInfo
        {
            public UInt16 lineNumber;
            public UInt16 address;
            public string raw;
        };

        public class SourceMap
        {
            public List<InstructionInfo> instructions = new List<InstructionInfo>();
            public Labels labels = new Labels();
        };

        public class Instruction
        {
            public string opCode;
            public List<string> args = new List<string>();


            internal void ensureNoArgs()
            {
                if (args.Count > 0)
                    throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.expectedNoArguments].Replace("{extra}", $"{this.opCode} ${this.args}"));
            }

            internal string singleArg()
            {
                if (args.Count != 1)
                    throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.expectedOneArgument].Replace("{extra}", $"{this.opCode} ${this.args}"));

                return args[0];
            }

            internal string[] twoArguments()
            {
                if (args.Count != 2)
                    throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.expectedTwoArguments].Replace("{extra}", $"{this.opCode} ${this.args}"));

                return args.ToArray();
            }

        };

        public class DebugInfo
        {
            public UInt16 address;
            public InstructionInfo info;
        };

        public class Target
        {
            public byte[] exe;
            public DebugInfo[] pdb;
        };
        /// <summary>
        /// Assemble a .slede file to .s8 file
        /// </summary>
        /// <param name="sledeFile"></param>
        /// <returns>Compiled memory</returns>
        public byte[] AssembleFile(string sledeFile, string s8file="")
        {
            //if (s8file.Length == 0)
            //{
            //    s8file = sledeFile;
            //    s8file.Replace(".asm", "", StringComparison.InvariantCultureIgnoreCase);
            //    s8file= s8file+ ".s8";
            //}

            var sledeTekst = File.ReadAllText(sledeFile);
            Console.WriteLine($"Lest sledetekst fra {sledeFile}, {sledeTekst.Length} tegn");
            Console.WriteLine("Compilerere...");

            var result = assemble(sledeTekst);

            Console.WriteLine($"Compiled OK! {result.pdb.Length} instructions");

            if (s8file.Length > 0)
            {
                Console.WriteLine($"Skriver S8 file {s8file}");
                File.WriteAllBytes(s8file, result.exe);
            }

            return result.exe;
        }

        public enum ERROR_MESSAGE_ID { expectedNoArguments, expectedOneArgument, expectedTwoArguments, unexpectedToken, invalidRegistry, invalidData };


        public static string[] ERROR_MESSAGE = {
            "Forventet ingen argumenter. {extra}",
            "Forventet ett argument. {extra}",
            "Forventet to argumenter. {extra}",
            "Skjønner ikke hva dette er: '{token}'",
            "Ugyldig register: '{reg}'",
            "Ugyldig .DATA format: '{data}'"
        };

        public byte[] AssembleStatement(string statement)
        {
            var map = Preprosess(statement);

            var instr = tokenize(map.instructions[0].raw);
            var bArray = Translate(instr, map.labels);

            return bArray;
        }
        /// <summary>
        /// "label" | "instruction" | "data" | "comment" | "whitespace" {
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public string classify(string line)
        {
            //line = line.Trim();

            if (line.Length == 0) return "whitespace";

            if (Regex.Match(line, @"^;.*$").Success) return "comment";

            if (Regex.Match(line, @"^[0-9a-zA-ZæøåÆØÅ\-_]+:$").Success) return "label";

            if (Regex.Match(line, @".DATA [x0-9a-fA-F, ]*$").Success) return "data";

            return "instruction";
        }

        public SourceMap Preprosess(string sourceCode)
        {
            SourceMap map = new SourceMap();
            UInt16 address = 0;
            UInt16 lineNo = 0;
            var all_lines = sourceCode.Split("\n");
            foreach (var current in all_lines)
            {
                // (prev, current, lineNumber) =>
                // const { instructions, labels } = prev;
                string line = current.Trim();
                switch (classify(line))
                {
                    case "label":
                        map.labels.addLabelAndAddress(line.Substring(0, line.Length - 1), address);
                        //labels[line.slice(0, -1)] = address;
                        //return { instructions, labels };
                        break;
                    case "data":
                        map.instructions.Add(new InstructionInfo() { lineNumber = lineNo, address = address, raw = line });
                        address += (UInt16)tokenize(line).args.Count;
                        break;
                    case "instruction":
                        map.instructions.Add(new InstructionInfo() { lineNumber = lineNo, address = address, raw = line });
                        address += 2;
                        break;
                    default:
                        break;
                }
                lineNo++;
            }

            return map;
        }
        Instruction tokenize(string raw)
        {
            var instr = new Instruction();
            var commentsRemoved = raw.Trim().Split(";")[0];
            var splitSpace = commentsRemoved.Split(" ");
            instr.opCode = splitSpace[0].Trim();

            //splitSpace.AsQueryable().Where((a, index) => a.Length > 0 && index > 1).Join("").Split(",");
            //var merge = splitSpace.AsQueryable().Where((a, index) => a.Length > 0 && index > 1); //.Join("").Split(",");

            string merge = string.Empty;
            for (int i=1;i<splitSpace.Length;i++)
            {
                merge += splitSpace[i];
            }

            var splitComma = merge.Split(",");
            foreach (string split in splitComma)
            {
                var splitTrimmed = split.Trim();
                if (splitTrimmed.Length > 0)
                {
                    instr.args.Add(splitTrimmed);
                }
            }
            

            return instr;
            /*
             * 
                const commentsRemoved = raw.trim().split(";")[0];
                const [opCode, ...rest] = commentsRemoved
                    .split(" ")
                    .map((x) => x.trim())
                    .filter((x) => x.length > 0);
                const args = (rest || [])
                    .join("")
                    .split(",")
                    .map((x) => x.trim())
                    .filter((x) => x.length > 0);
                return { opCode, args };
            */
        }




        public byte[] Translate(Instruction instruction, Labels labels)
        {
            //const { opCode, args } = instruction;


            if (instruction.opCode == ".DATA")
            {
                return TranslateData(instruction);
            }



            string[] aluOps = new string[] {
                "OG",
                "ELLER",
                "XELLER",
                "VSKIFT",
                "HSKIFT",
                "PLUSS",
                "MINUS"
                };

            string[] cmpOps = new string[] { "LIK", "ULIK", "ME", "MEL", "SE", "SEL" };

            UInt16 returnCode = 0x00;

            switch (instruction.opCode)
            {
                case "STOPP":
                    instruction.ensureNoArgs();
                    returnCode= writeHalt();
                    break;
                case "SETT":
                    returnCode= writeSet(instruction.twoArguments());
                    break;

                case "FINN":
                    returnCode = writeLocate(instruction.singleArg(), labels);
                    break;
                case "LAST":
                    returnCode = writeLoad(instruction.singleArg());
                    break;

                case "LAGR":
                    returnCode = writeStore(instruction.singleArg());
                    break;

                // ALU
                case "OG":
                case "ELLER":
                case "XELLER":
                case "VSKIFT":
                case "HSKIFT":
                case "PLUSS":
                case "MINUS":
                    byte bOps = (byte)Array.IndexOf(aluOps, instruction.opCode);
                    returnCode= writeAlu(bOps, instruction.twoArguments());
                    break;

                // I/O
                case "LES":
                    returnCode = writeRead(instruction.singleArg());
                    break;

                case "SKRIV":
                    returnCode= writeWrite(instruction.singleArg());
                    break;

                // CMP
                case "LIK":
                case "ULIK":
                case "ME":
                case "MEL":
                case "SE":
                case "SEL":
                    byte bCode = (byte)Array.IndexOf(cmpOps, instruction.opCode);
                    returnCode = writeCmp(bCode, instruction.twoArguments());
                    break;

                case "HOPP":
                    returnCode = writeJmp(8, instruction.singleArg(), labels);
                    break;

                case "BHOPP":
                    returnCode = writeJmp(9, instruction.singleArg(), labels);
                    break;

                case "TUR":
                    returnCode = writeCall(instruction.singleArg(), labels);
                    break;

                case "RETUR":
                    instruction.ensureNoArgs();
                    returnCode = writeRet();
                    break;

                case "NOPE":
                    instruction.ensureNoArgs();
                    returnCode = writeNop();
                    break;

                default:
                    throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.unexpectedToken].Replace("{token}", $"{instruction.opCode}"));                    
            }

            return Uint8Array(returnCode); // will never happen - just put here to make compiler silent
        }


        /// <summary>
        /// Translate the ".DATA" statement to byte array
        /// </summary>
        /// <param name="instruction"></param>
        /// <returns></returns>
        private byte[] TranslateData(Instruction instruction)
        {
            int len = instruction.args.Count;

            // using memory stream instead of array as its easier for dumping string data
            using (MemoryStream ms = new MemoryStream())
            {

                for (int i = 0; i < len; i++)
                {
                    var arg = instruction.args[i];
                    if (arg.Substring(0, 1) == @"'")
                    {
                        // Handle string input
                        string data = arg.Substring(1);
                        int endingPoint = data.IndexOf("'");

                        if (endingPoint < 1)
                            throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.invalidData].Replace("{data}", arg));
                        data = data.Substring(0, endingPoint);

                        ms.Write(Encoding.ASCII.GetBytes(data));
                    }
                    else
                        ms.WriteByte((byte)getVal(arg));
                }

                return ms.ToArray();
            }
        }

        public Target assemble(string sourceCode)
        {
            byte[] magic = new byte[] { 0x2E, 0x53, 0x4C, 0x45, 0x44, 0x45, 0x38 };

            Target t = new Target();
            var sourceMap = Preprosess(sourceCode);


            using (MemoryStream ms = new MemoryStream())
            {
                // Add magic first
                ms.Write(magic);

                // Then instructions
                foreach (InstructionInfo instr in sourceMap.instructions)
                {
                    var instruction = tokenize(instr.raw);
                    ms.Write(Translate(instruction, sourceMap.labels));
                }
                t.exe = ms.ToArray();
            }


            /// Map debug info
            t.pdb = new DebugInfo[sourceMap.instructions.Count];
            for (int i = 0; i < sourceMap.instructions.Count; i++) 
            {
                t.pdb[i] = new DebugInfo();
                t.pdb[i].address = sourceMap.instructions[i].address;
                t.pdb[i].info = sourceMap.instructions[i];
            };                        

            return t;
        }

        /*
        public concat(buffers)
        {
            int  totalLength = buffers.reduce((acc, value) => acc + value.length, 0);

            if (!buffers.length) return new Uint8Array([]);
                const result = new Uint8Array(totalLength);

            int length = 0;
            for (const array of buffers) 
            {
                result.set(array, length);
                length += array.length;
            }

            return result;
        }
        */
        byte[] Uint8Array(UInt16 val)
        {
            byte[] a = new byte[2];
            a[0] = (byte)((val & 0x00FF));
            a[1] = (byte)((val & 0xFF00) >> 8);

            return a;
        }

        UInt16 nibs(byte nib1, byte nib2, byte nib3, byte nib4)
        {
            UInt16 n = (UInt16)(nib1 | (nib2 << 4) | (nib3 << 8) | (nib4 << 12));
            return n;

        }

        UInt16 nibsByte(byte nib1, byte nib2, byte b)
        {
            UInt16 n = (UInt16)(nib1 | (nib2 << 4) | (b << 8));
            return n;
        }

        UInt16 nibVal(UInt16 nib, UInt16 val)
        {
            return (UInt16)(nib | (val << 4));
        }

        UInt16 parseVal(string valStr)
        {
            if (valStr.StartsWith("0x"))
            {
                return (UInt16)int.Parse(valStr.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            return (UInt16)int.Parse(valStr);
        }

        bool isVal(string valStr)
        {
            //if (isNaN(+valStr)) return false;
            try
            {
                parseVal(valStr);
                return true;
            }
            catch
            {
                return false;
            }
        }

        UInt16 getVal(string valStr)
        {
            return (UInt16)parseVal(valStr);
        }

        byte getReg(string regStr)
        {
            if (!regStr.StartsWith('r')) 
                throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.invalidRegistry].Replace("{reg}", regStr));

            byte regNum = (byte)parseVal(regStr.Substring(1));

            if (regNum< 0 || regNum> 15)
                throw new S8AssemblerException(ERROR_MESSAGE[(int)ERROR_MESSAGE_ID.invalidRegistry].Replace("{reg}", regStr));

            return regNum;
        }

        UInt16 getAddr(string addrStr, Labels labels)
        {
            if (isVal(addrStr))
            {
                return getVal(addrStr);
            }

            UInt16 address = labels.mapLabelToAddress(addrStr);

            if (address == UNDEFINED)
            {
                //throw ERROR_MESSAGE.unexpectedToken(addrStr); //TOFIX
            }

            return address;
        }
        #region Write byte for instructions
        UInt16 writeHalt()
        {
            return (UInt16)(0);
        }

        // SETT r1, 44
        UInt16 writeSet(string[] args)
        {

            string reg1 = args[0];
            string regOrValue = args[1];            

            byte reg1Num = getReg(reg1);

            if (isVal(regOrValue))
            {
                byte value = (byte)getVal(regOrValue);
                return nibsByte(1, reg1Num, value);
            }
            else
            {
                byte  reg2Num = getReg(regOrValue);
                return nibsByte(2, reg1Num, reg2Num);
            }
        }


        UInt16 writeLocate(string arg, Labels labels)
        {
            UInt16 addr = getAddr(arg, labels);
            return nibVal(3, addr);
        }

        UInt16 writeLoad(string regStr)
        {
            byte regNum = getReg(regStr);
            return nibs(4, 0, regNum, 0);
        }

        UInt16 writeStore(string reg)
        {
            byte regNum = getReg(reg);
            return nibs(4, 1, regNum, 0);
        }

        UInt16 writeAlu(byte aluOp, string[] args)
        {
            string reg1 = args[0];
            string reg2 = args[1];
            //const [reg1, reg2] = args;

            byte reg1Num = getReg(reg1);
            byte reg2Num = getReg(reg2);
            return nibs(5, aluOp, reg1Num, reg2Num);
        }

        UInt16 writeRead(string arg)
        {
            string reg = arg.Trim();
            byte regNum = getReg(reg);
            return nibs(6, 0, regNum, 0);
        }


        UInt16 writeWrite(string arg)
        {
            string reg = arg.Trim();
            byte regNum = getReg(reg);
            return nibs(6, 1, regNum, 0);
        }

        UInt16 writeCmp(byte cmpOp, string[] args)
        {
            string reg1 = args[0];
            string reg2 = args[1];
            //const [reg1, reg2] = args;

            byte reg1Num = getReg(reg1);
            byte reg2Num = getReg(reg2);
            return nibs(7, cmpOp, reg1Num, reg2Num);
        }

        UInt16 writeJmp(byte jmpOp, string arg, Labels labels)
        {
            return nibVal(jmpOp, getAddr(arg, labels));
        }

        UInt16 writeCall(string arg, Labels labels)
        {
            return nibVal(0xa, getAddr(arg, labels));
        }

        UInt16 writeRet()
        {
            return (UInt16)0xb;
        }

        UInt16 writeNop()
        {
            return (UInt16)0xc;
        }

        #endregion 


    }

    public class S8AssemblerException : SystemException
    {
        public S8AssemblerException() : base() { }
        public S8AssemblerException(string message) : base(message) { }
    }



}

