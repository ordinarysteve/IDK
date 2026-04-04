using System;
using System.Collections.Generic;

public delegate void PinChangeListener(int pin, bool state);

public class MemoryManager
{
    public readonly byte[] flash;
    public readonly byte[] sram;
    public readonly byte[] eeprom;
    private readonly int FLASH_SIZE = 32 * 1024; // 32KB
    private readonly int SRAM_SIZE = 2 * 1024; // 2KB
    private readonly int EEPROM_SIZE = 1 * 1024; // 1KB
    private List<PinChangeListener> pinListeners = new List<PinChangeListener>();

    public MemoryManager()
    {
        this.flash = new byte[this.FLASH_SIZE];
        this.sram = new byte[this.SRAM_SIZE];
        this.eeprom = new byte[this.EEPROM_SIZE];
    }

    public void onPinChange(PinChangeListener listener)
    {
        this.pinListeners.Add(listener);
    }

    /**
     * Reads a single byte from SRAM.
     * @param addr The memory address relative to the start of SRAM (0 to 2047).
     */
    public int read8(int addr)
    {
        this.checkSRAMBounds(addr, 1);
        return this.sram[addr];
    }

    /**
     * Writes a single byte to SRAM.
     * @param addr The memory address relative to the start of SRAM (0 to 2047).
     * @param val The 8-bit value to write.
     */
    public void write8(int addr, int val)
    {
        this.checkSRAMBounds(addr, 1);
        if (addr == 0x25)
        {
            // PORTB
            int oldVal = this.sram[addr];
            int newVal = val & 0xff;
            int changedBits = oldVal ^ newVal;
            if (changedBits != 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (((changedBits >> i) & 1) != 0)
                    {
                        int pin = i + 8;
                        bool state = ((newVal >> i) & 1) != 0;
                        foreach (var listener in this.pinListeners)
                        {
                            listener(pin, state);
                        }
                    }
                }
            }
        }
        this.sram[addr] = (byte)(val & 0xff);
    }

    /**
     * Reads a 16-bit word from SRAM (Little-Endian).
     * @param addr The memory address relative to the start of SRAM.
     */
    public int read16(int addr)
    {
        this.checkSRAMBounds(addr, 2);
        int low = this.sram[addr];
        int high = this.sram[addr + 1];
        return (high << 8) | low;
    }

    /**
     * Writes a 16-bit word to SRAM (Little-Endian).
     * @param addr The memory address relative to the start of SRAM.
     * @param val The 16-bit value to write.
     */
    public void write16(int addr, int val)
    {
        this.checkSRAMBounds(addr, 2);
        this.sram[addr] = (byte)(val & 0xff);
        this.sram[addr + 1] = (byte)((val >> 8) & 0xff);
    }

    /**
     * Validates that the requested memory access is within SRAM limits.
     */
    private void checkSRAMBounds(int addr, int size)
    {
        if (addr < 0 || addr + size > this.SRAM_SIZE)
        {
            throw new Exception($"SRAM Access Out of Bounds: address 0x{addr:X}, size {size}");
        }
    }
}

public class RegisterFile
{
    private readonly byte[] r = new byte[32];
    public int pc = 0;
    public int sp = 0;
    public int sreg = 0;

    public int get(int index)
    {
        return this.r[index];
    }

    public void set(int index, int value)
    {
        this.r[index] = (byte)(value & 0xff);
    }

    public int r0
    {
        get => this.r[0];
        set => this.r[0] = (byte)(value & 0xff);
    }
    public int r1
    {
        get => this.r[1];
        set => this.r[1] = (byte)(value & 0xff);
    }
    public int r2
    {
        get => this.r[2];
        set => this.r[2] = (byte)(value & 0xff);
    }
    public int r3
    {
        get => this.r[3];
        set => this.r[3] = (byte)(value & 0xff);
    }
    public int r4
    {
        get => this.r[4];
        set => this.r[4] = (byte)(value & 0xff);
    }
    public int r5
    {
        get => this.r[5];
        set => this.r[5] = (byte)(value & 0xff);
    }
    public int r6
    {
        get => this.r[6];
        set => this.r[6] = (byte)(value & 0xff);
    }
    public int r7
    {
        get => this.r[7];
        set => this.r[7] = (byte)(value & 0xff);
    }
    public int r8
    {
        get => this.r[8];
        set => this.r[8] = (byte)(value & 0xff);
    }
    public int r9
    {
        get => this.r[9];
        set => this.r[9] = (byte)(value & 0xff);
    }
    public int r10
    {
        get => this.r[10];
        set => this.r[10] = (byte)(value & 0xff);
    }
    public int r11
    {
        get => this.r[11];
        set => this.r[11] = (byte)(value & 0xff);
    }
    public int r12
    {
        get => this.r[12];
        set => this.r[12] = (byte)(value & 0xff);
    }
    public int r13
    {
        get => this.r[13];
        set => this.r[13] = (byte)(value & 0xff);
    }
    public int r14
    {
        get => this.r[14];
        set => this.r[14] = (byte)(value & 0xff);
    }
    public int r15
    {
        get => this.r[15];
        set => this.r[15] = (byte)(value & 0xff);
    }
    public int r16
    {
        get => this.r[16];
        set => this.r[16] = (byte)(value & 0xff);
    }
    public int r17
    {
        get => this.r[17];
        set => this.r[17] = (byte)(value & 0xff);
    }
    public int r18
    {
        get => this.r[18];
        set => this.r[18] = (byte)(value & 0xff);
    }
    public int r19
    {
        get => this.r[19];
        set => this.r[19] = (byte)(value & 0xff);
    }
    public int r20
    {
        get => this.r[20];
        set => this.r[20] = (byte)(value & 0xff);
    }
    public int r21
    {
        get => this.r[21];
        set => this.r[21] = (byte)(value & 0xff);
    }
    public int r22
    {
        get => this.r[22];
        set => this.r[22] = (byte)(value & 0xff);
    }
    public int r23
    {
        get => this.r[23];
        set => this.r[23] = (byte)(value & 0xff);
    }
    public int r24
    {
        get => this.r[24];
        set => this.r[24] = (byte)(value & 0xff);
    }
    public int r25
    {
        get => this.r[25];
        set => this.r[25] = (byte)(value & 0xff);
    }
    public int r26
    {
        get => this.r[26];
        set => this.r[26] = (byte)(value & 0xff);
    }
    public int r27
    {
        get => this.r[27];
        set => this.r[27] = (byte)(value & 0xff);
    }
    public int r28
    {
        get => this.r[28];
        set => this.r[28] = (byte)(value & 0xff);
    }
    public int r29
    {
        get => this.r[29];
        set => this.r[29] = (byte)(value & 0xff);
    }
    public int r30
    {
        get => this.r[30];
        set => this.r[30] = (byte)(value & 0xff);
    }
    public int r31
    {
        get => this.r[31];
        set => this.r[31] = (byte)(value & 0xff);
    }

    public int x
    {
        get => this.r[26] | (this.r[27] << 8);
        set
        {
            this.r[26] = (byte)(value & 0xff);
            this.r[27] = (byte)((value >> 8) & 0xff);
        }
    }
    public int y
    {
        get => this.r[28] | (this.r[29] << 8);
        set
        {
            this.r[28] = (byte)(value & 0xff);
            this.r[29] = (byte)((value >> 8) & 0xff);
        }
    }
    public int z
    {
        get => this.r[30] | (this.r[31] << 8);
        set
        {
            this.r[30] = (byte)(value & 0xff);
            this.r[31] = (byte)((value >> 8) & 0xff);
        }
    }
}

public class StatusRegister
{
    private RegisterFile _regs;

    public StatusRegister(RegisterFile regs) => _regs = regs;

    public int C
    {
        get => (_regs.sreg >> 0) & 1;
        set => _regs.sreg = (_regs.sreg & ~(1 << 0)) | ((value & 1) << 0);
    }
    public int Z
    {
        get => (_regs.sreg >> 1) & 1;
        set => _regs.sreg = (_regs.sreg & ~(1 << 1)) | ((value & 1) << 1);
    }
    public int N
    {
        get => (_regs.sreg >> 2) & 1;
        set => _regs.sreg = (_regs.sreg & ~(1 << 2)) | ((value & 1) << 2);
    }
    public int V
    {
        get => (_regs.sreg >> 3) & 1;
        set => _regs.sreg = (_regs.sreg & ~(1 << 3)) | ((value & 1) << 3);
    }
    public int S
    {
        get => (_regs.sreg >> 4) & 1;
        set => _regs.sreg = (_regs.sreg & ~(1 << 4)) | ((value & 1) << 4);
    }
    public int H
    {
        get => (_regs.sreg >> 5) & 1;
        set => _regs.sreg = (_regs.sreg & ~(1 << 5)) | ((value & 1) << 5);
    }
    public int T
    {
        get => (_regs.sreg >> 6) & 1;
        set => _regs.sreg = (_regs.sreg & ~(1 << 6)) | ((value & 1) << 6);
    }
    public int I
    {
        get => (_regs.sreg >> 7) & 1;
        set => _regs.sreg = (_regs.sreg & ~(1 << 7)) | ((value & 1) << 7);
    }
}

public class ALU
{
    private StatusRegister sreg;

    public ALU(StatusRegister sreg)
    {
        this.sreg = sreg;
    }

    public int add(int rd, int rr)
    {
        int result = (rd + rr) & 0xff;
        int rd3 = (rd >> 3) & 1;
        int rr3 = (rr >> 3) & 1;
        int r3 = (result >> 3) & 1;
        int rd7 = (rd >> 7) & 1;
        int rr7 = (rr >> 7) & 1;
        int r7 = (result >> 7) & 1;

        this.sreg.H = ((rd3 & rr3) | (rr3 & ~r3) | (~r3 & rd3)) != 0 ? 1 : 0;
        this.sreg.V = ((rd7 & rr7 & ~r7) | (~rd7 & ~rr7 & r7)) != 0 ? 1 : 0;
        this.sreg.N = r7;
        this.sreg.Z = result == 0 ? 1 : 0;
        this.sreg.C = ((rd7 & rr7) | (rr7 & ~r7) | (~r7 & rd7)) != 0 ? 1 : 0;
        this.sreg.S = this.sreg.N ^ this.sreg.V;

        return result;
    }

    public int sub(int rd, int rr)
    {
        int result = (rd - rr) & 0xff;
        int rd3 = (rd >> 3) & 1;
        int rr3 = (rr >> 3) & 1;
        int r3 = (result >> 3) & 1;
        int rd7 = (rd >> 7) & 1;
        int rr7 = (rr >> 7) & 1;
        int r7 = (result >> 7) & 1;

        this.sreg.H = ((~rd3 & rr3) | (rr3 & r3) | (r3 & ~rd3)) != 0 ? 1 : 0;
        this.sreg.V = ((rd7 & ~rr7 & ~r7) | (~rd7 & rr7 & r7)) != 0 ? 1 : 0;
        this.sreg.N = r7;
        this.sreg.Z = result == 0 ? 1 : 0;
        this.sreg.C = ((~rd7 & rr7) | (rr7 & r7) | (r7 & ~rd7)) != 0 ? 1 : 0;
        this.sreg.S = this.sreg.N ^ this.sreg.V;

        return result;
    }

    public int and(int rd, int rr)
    {
        int result = rd & rr & 0xff;
        this.updateLogicalFlags(result);
        return result;
    }

    public int or(int rd, int rr)
    {
        int result = (rd | rr) & 0xff;
        this.updateLogicalFlags(result);
        return result;
    }

    public int xor(int rd, int rr)
    {
        int result = (rd ^ rr) & 0xff;
        this.updateLogicalFlags(result);
        return result;
    }

    private void updateLogicalFlags(int result)
    {
        int r7 = (result >> 7) & 1;
        this.sreg.V = 0;
        this.sreg.N = r7;
        this.sreg.Z = result == 0 ? 1 : 0;
        this.sreg.S = this.sreg.N ^ this.sreg.V;
    }
}

public class AvrCore
{
    public readonly MemoryManager memory;
    public readonly RegisterFile registers;
    public readonly ALU alu;
    private readonly StatusRegister sregProxy;

    public AvrCore()
    {
        this.memory = new MemoryManager();
        this.registers = new RegisterFile();
        this.sregProxy = new StatusRegister(this.registers);
        this.alu = new ALU(this.sregProxy);
    }
}
