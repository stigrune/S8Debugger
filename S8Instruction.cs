﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace S8Debugger
{
    public class S8Instruction
    {

        // Helper bytes to keep the original values that created the Instruction
        public byte Opcode;
        public byte Param; 

        // decoded fields from opcode + param
        public int operationClass;
        public int operation;
        public int address;
        public int value;
        public int argument1;
        public int argument2;

        public string DecodedInstruction;
        public bool ValidInstruction;
        public string ErrorMessage;

        public S8Instruction(byte opcode, byte param)
        {
            init(opcode, param);
        }
        public void init(byte opcode, byte param)
        {
            ValidInstruction = false;

            Opcode = opcode;
            Param = param;


            int instruction = opcode | (param << 8);

            operationClass = instruction & 0xf;
            operation = (instruction >> 4) & 0xf;
            address = instruction >> 4;
            value = instruction >> 8;
            argument1 = (instruction >> 8) & 0xf;
            argument2 = (instruction >> 12) & 0xf;
        }

        public string DefaultDecodeError()
        {
            return "Unknown operation [" + operation + "] in operationClass 0x" + operationClass;
        }

        public void DecodeInstruction()
        {
            DecodedInstruction = "; NOT DECODED";
            switch (operationClass)
            {

                case 0x0:
                    switch (operation)
                    {
                        case 0x0: // STOPP
                            DecodedInstruction = "STOPP";
                            ValidInstruction = true;
                            break;
                        default:
                            ErrorMessage = DefaultDecodeError();
                            break;
                    }
                    break;

                case 0x1: // 0001 0001
                    DecodedInstruction = "SETT r" + operation.ToString() + ", " + value;
                    ValidInstruction = true;
                    break;

                case 0x2: // nnnn 0010
                    DecodedInstruction = "SETT r" + operation.ToString() + ", r" + argument1;
                    ValidInstruction = true;
                    break;
                case 0x3:

                    int regs1 = (address & 0x0f00) >> 8;
                    int regs0 = address & 0xff;
                    DecodedInstruction = "FINN m" + address.ToString("X3"); //+ " ; r0 = " + regs0.ToString("X2") + "  r1 = " + regs1.ToString("X2");                    
                    ValidInstruction = true;
                    break;

                case 0x04: //Last  nn

                    switch (operation)
                    {
                        case 0x0:
                            DecodedInstruction = "LAST r" + argument1;
                            ValidInstruction = true;
                            break;
                        case 0x1:
                            DecodedInstruction = "LAGR r" + argument1; ;
                            ValidInstruction = true;
                            break;

                        default:
                            ErrorMessage = DefaultDecodeError();
                            break;
                    }
                    break;
                case 0x05: // Logic

                    string logicFunction = "";
                    switch (operation)
                    {
                        case 0x0:
                            logicFunction = "OG ";
                            ValidInstruction = true;
                            break;
                        case 0x1:
                            logicFunction = "ELLER ";
                            ValidInstruction = true;
                            break;
                        case 0x2:
                            logicFunction = "XELLER ";
                            ValidInstruction = true;
                            break;
                        case 0x3:
                            logicFunction = "VSKIFT ";
                            ValidInstruction = true;
                            break;
                        case 0x4:
                            logicFunction = "HSKIFT ";
                            ValidInstruction = true;
                            break;
                        case 0x5:
                            logicFunction = "PLUSS ";
                            ValidInstruction = true;
                            break;
                        case 0x6:
                            logicFunction = "MINUS ";
                            ValidInstruction = true;
                            break;

                        default:
                            ErrorMessage = DefaultDecodeError();
                            break;
                    }

                    DecodedInstruction = logicFunction + "r" + argument1 + ", r" + argument2;
                    break;

                case 0x6: // LES & SKRIV
                    switch (operation)
                    {
                        case 0x0: //LES
                            DecodedInstruction = "LES r" + argument1;
                            ValidInstruction = true;
                            break;
                        case 0x1: //LES
                            DecodedInstruction = "SKRIV r" + argument1;
                            ValidInstruction = true;
                            break;
                        default:
                            ErrorMessage = DefaultDecodeError();
                            break;

                    }
                    break;
                case 0x07: //LIK p
                    string cmpFunction = "";
                    switch (operation)
                    {
                        case 0x0:
                            cmpFunction = "LIK ";
                            ValidInstruction = true;
                            break;
                        case 0x1:
                            cmpFunction = "ULIK ";
                            ValidInstruction = true;
                            break;
                        case 0x2:
                            cmpFunction = "ME ";
                            ValidInstruction = true;
                            break;
                        case 0x3:
                            cmpFunction = "MEL ";
                            ValidInstruction = true;
                            break;
                        case 0x4:
                            cmpFunction = "SE ";
                            ValidInstruction = true;
                            break;
                        case 0x5:
                            cmpFunction = "SEL ";
                            ValidInstruction = true;
                            break;
                        default:
                            ErrorMessage = DefaultDecodeError();
                            break;

                    }
                    DecodedInstruction = cmpFunction + "r" + argument1 + ", r" + argument2;
                    break;
                    
                case 0x8:                    
                    DecodedInstruction = "HOPP a" + address.ToString("X3"); // address

                    ValidInstruction = true;
                    break;
                case 0x9:                    
                    DecodedInstruction = "BHOPP a" + address.ToString("X3");

                    ValidInstruction = true;
                    break;

                case 0x0A:                    
                    DecodedInstruction = "TUR a" + " 0x" + address.ToString("X3");
                    ValidInstruction = true;
                    break;
                case 0x0B:
                    switch (operation)
                    {
                        case 0x0: // RETUR
                            DecodedInstruction = "RETUR";
                            ValidInstruction = true;
                            break;
                        default:
                            ErrorMessage = DefaultDecodeError();
                            break;
                    }
                    break;
                case 0x0C:
                    switch (operation)
                    {
                        case 0x0: // NOPE
                            DecodedInstruction = "NOPE";
                            ValidInstruction = true;
                            break;

                        default:
                            DecodedInstruction = DefaultDecodeError();
                            break;
                    }
                    break;


                default:
                    DecodedInstruction = DefaultDecodeError();
                    break;


            }

            
        }

        public string Instruction2Text(int currentAddress, bool showAddress)
        {
            string sHexAddress = currentAddress.ToString("X3");
            string outStr = string.Empty;

            if (ValidInstruction)
            {
                if (!showAddress)
                    outStr += "a" + sHexAddress + ": \r\n";
            }
            else
            {
                if (!showAddress)
                    outStr += "m" + sHexAddress + ": \r\n";
            }

            if (showAddress)
            {
                string sOpcode = Opcode.ToString("X2");
                string sParam = Param.ToString("X2");
                outStr += "A[" + sHexAddress + "] | I[" + sOpcode + " " + sParam + "] ";
            }


            if (ValidInstruction)
            {
                outStr += DecodedInstruction;
            }
            else
            {
                string data = ".DATA 0x" + Opcode.ToString("X2");
                outStr += data + " ; " + ErrorMessage;
            }

            return outStr;
        }

    };
}
