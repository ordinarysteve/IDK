using System.Collections.Generic;

public delegate void InstructionExec(AvrCore core, int opcode);

public class Instruction
{
    public ushort Mask;
    public ushort Match;
    public InstructionExec Exec;
}

public class InstructionDecoder
{
    private List<Instruction> instructionMap = new List<Instruction>();
    private AvrCore core;

    public InstructionDecoder(AvrCore core)
    {
        this.core = core;
        Add(0xffff, 0x9508, RET);
        Add(0xffff, 0x0000, NOP);
        Add(0xfe0f, 0x920f, PUSH);
        Add(0xfe0f, 0x900f, POP);
        // BRANCHING
        Add(0xfc07, 0xf401, BRNE); // BRBC 1
        Add(0xfc07, 0xf001, BREQ); // BRBS 1
        Add(0xfc07, 0xf400, BRCC); // BRBC 0
        Add(0xfc07, 0xf000, BRCS); // BRBS 0
        Add(0xfc07, 0xf004, BRLT); // BRBS 4
        Add(0xfc07, 0xf404, BRGE); // BRBC 4
        Add(0xfc00, 0x0c00, ADD);
        Add(0xf000, 0xe000, LDI);
        Add(0xf000, 0xd000, RCALL);
        Add(0xf000, 0xc000, RJMP);
        Add(0xf000, 0x3000, CPI);
        // 32-BIT
        Add(0xfe0f, 0x9200, STS);
        Add(0xfe0f, 0x9000, LDS);
        // POINTERS: X
        Add(0xfe0f, 0x920c, ST_X);
        Add(0xfe0f, 0x920d, ST_X_INC);
        Add(0xfe0f, 0x920e, ST_X_DEC);
        Add(0xfe0f, 0x900c, LD_X);
        Add(0xfe0f, 0x900d, LD_X_INC);
        Add(0xfe0f, 0x900e, LD_X_DEC);
        // POINTERS: Y
        Add(0xfe0f, 0x9209, ST_Y_INC);
        Add(0xfe0f, 0x920a, ST_Y_DEC);
        Add(0xfe0f, 0x9009, LD_Y_INC);
        Add(0xfe0f, 0x900a, LD_Y_DEC);
        // POINTERS: Z
        Add(0xfe0f, 0x9201, ST_Z_INC);
        Add(0xfe0f, 0x9202, ST_Z_DEC);
        Add(0xfe0f, 0x9001, LD_Z_INC);
        Add(0xfe0f, 0x9002, LD_Z_DEC);
        // POINTERS: LDD / STD (includes ST Y/Z and LD Y/Z with q=0)
        Add(0xd208, 0x8208, STD_Y);
        Add(0xd208, 0x8200, STD_Z);
        Add(0xd208, 0x8008, LDD_Y);
        Add(0xd208, 0x8000, LDD_Z);
        // I/O INSTRUCTIONS
        Add(0xf800, 0xb800, OUT);
        Add(0xf800, 0xb000, IN);
        // BIT-OPS
        Add(0xff00, 0x9a00, SBI);
        Add(0xff00, 0x9800, CBI);
        Add(0xff00, 0x9b00, SBIS);
        Add(0xff00, 0x9900, SBIC);
        // LOGIC
        Add(0xfc00, 0x2000, AND);
        Add(0xfc00, 0x2800, OR);
        Add(0xfc00, 0x2400, EOR);
        Add(0xfe0f, 0x9400, COM);
        Add(0xfe0f, 0x9401, NEG);
        Add(0xff00, 0x0100, MOVW);
        // ADVANCED MATH
        Add(0xfc00, 0x1c00, ADC);
        Add(0xfc00, 0x0800, SBC);
        Add(0xfc00, 0x1400, CP);
        Add(0xfc00, 0x1000, CPC);
    }

    private void Add(ushort mask, ushort match, InstructionExec exec)
    {
        this.instructionMap.Add(
            new Instruction
            {
                Mask = mask,
                Match = match,
                Exec = exec,
            }
        );
    }

    public void Step()
    {
        int pc = this.core.registers.pc;
        int low = this.core.memory.flash[pc];
        int high = this.core.memory.flash[pc + 1];
        int opcode = (high << 8) | low;
        this.core.registers.pc += 2;

        for (int i = 0; i < this.instructionMap.Count; i++)
        {
            var instruction = this.instructionMap[i];
            if ((opcode & instruction.Mask) == instruction.Match)
            {
                instruction.Exec(this.core, opcode);
                return;
            }
        }
        this.NOP(this.core, opcode);
    }

    private void NOP(AvrCore core, int opcode) { }

    private void LDI(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x0f;
        int Rd = 16 + d;
        int K = ((opcode >> 4) & 0xf0) | (opcode & 0x0f);
        core.registers.set(Rd, K);
    }

    private void ADD(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int r = ((opcode >> 5) & 0x10) | (opcode & 0x0f);
        int val_d = core.registers.get(d);
        int val_r = core.registers.get(r);
        core.registers.set(d, core.alu.add(val_d, val_r));
    }

    private void RJMP(AvrCore core, int opcode)
    {
        int k = opcode & 0x0fff;
        if ((k & 0x0800) != 0)
            k -= 0x1000;
        core.registers.pc += k * 2;
    }

    private void CPI(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x0f;
        int Rd = 16 + d;
        int K = ((opcode >> 4) & 0xf0) | (opcode & 0x0f);
        int val_d = core.registers.get(Rd);
        core.alu.sub(val_d, K);
    }

    private void BREQ(AvrCore core, int opcode)
    {
        if ((core.registers.sreg & 2) != 0)
        {
            int k = (opcode >> 3) & 0x7f;
            if ((k & 0x40) != 0)
                k -= 0x80;
            core.registers.pc += k * 2;
        }
    }

    private void BRNE(AvrCore core, int opcode)
    {
        if ((core.registers.sreg & 2) == 0)
        {
            int k = (opcode >> 3) & 0x7f;
            if ((k & 0x40) != 0)
                k -= 0x80;
            core.registers.pc += k * 2;
        }
    }

    private void PUSH(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int val_d = core.registers.get(d);
        core.memory.write8(core.registers.sp, (byte)val_d);
        core.registers.sp--;
    }

    private void POP(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        core.registers.sp++;
        int val = core.memory.read8(core.registers.sp);
        core.registers.set(d, val);
    }

    private void RCALL(AvrCore core, int opcode)
    {
        int k = opcode & 0x0fff;
        if ((k & 0x0800) != 0)
            k -= 0x1000;
        int pc = core.registers.pc;
        core.memory.write8(core.registers.sp, (byte)(pc & 0xff));
        core.registers.sp--;
        core.memory.write8(core.registers.sp, (byte)((pc >> 8) & 0xff));
        core.registers.sp--;
        core.registers.pc += k * 2;
    }

    private void RET(AvrCore core, int opcode)
    {
        core.registers.sp++;
        int high = core.memory.read8(core.registers.sp);
        core.registers.sp++;
        int low = core.memory.read8(core.registers.sp);
        core.registers.pc = (high << 8) | low;
    }

    private void STS(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int val = core.registers.get(d);
        int pc = core.registers.pc;
        int low = core.memory.flash[pc];
        int high = core.memory.flash[pc + 1];
        int addr = (high << 8) | low;
        core.registers.pc += 2;
        core.memory.write8(addr, (byte)val);
    }

    private void LDS(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int pc = core.registers.pc;
        int low = core.memory.flash[pc];
        int high = core.memory.flash[pc + 1];
        int addr = (high << 8) | low;
        core.registers.pc += 2;
        int val = core.memory.read8(addr);
        core.registers.set(d, val);
    }

    private void BRCS(AvrCore core, int opcode)
    {
        if ((core.registers.sreg & 1) != 0)
        {
            int k = (opcode >> 3) & 0x7f;
            if ((k & 0x40) != 0)
                k -= 0x80;
            core.registers.pc += k * 2;
        }
    }

    private void BRCC(AvrCore core, int opcode)
    {
        if ((core.registers.sreg & 1) == 0)
        {
            int k = (opcode >> 3) & 0x7f;
            if ((k & 0x40) != 0)
                k -= 0x80;
            core.registers.pc += k * 2;
        }
    }

    private void BRLT(AvrCore core, int opcode)
    {
        if ((core.registers.sreg & 16) != 0)
        {
            int k = (opcode >> 3) & 0x7f;
            if ((k & 0x40) != 0)
                k -= 0x80;
            core.registers.pc += k * 2;
        }
    }

    private void BRGE(AvrCore core, int opcode)
    {
        if ((core.registers.sreg & 16) == 0)
        {
            int k = (opcode >> 3) & 0x7f;
            if ((k & 0x40) != 0)
                k -= 0x80;
            core.registers.pc += k * 2;
        }
    }

    private void ADC(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int r = ((opcode >> 5) & 0x10) | (opcode & 0x0f);
        int Rd = core.registers.get(d);
        int Rr = core.registers.get(r);
        int C = core.registers.sreg & 1;
        int res = Rd + Rr + C;
        core.registers.set(d, res & 0xff);
        int Rd7 = (Rd >> 7) & 1;
        int Rr7 = (Rr >> 7) & 1;
        int R7 = (res >> 7) & 1;
        int Rd3 = (Rd >> 3) & 1;
        int Rr3 = (Rr >> 3) & 1;
        int R3 = (res >> 3) & 1;
        int sreg = core.registers.sreg;
        sreg &= ~0x3f;
        int H = (Rd3 & Rr3) | (Rr3 & (1 - R3)) | ((1 - R3) & Rd3);
        int V = (Rd7 & Rr7 & (1 - R7)) | ((1 - Rd7) & (1 - Rr7) & R7);
        int N = R7;
        int S = N ^ V;
        int C_flag = (Rd7 & Rr7) | (Rr7 & (1 - R7)) | ((1 - R7) & Rd7);
        int Z = (res & 0xff) == 0 ? 1 : 0;
        if (C_flag != 0)
            sreg |= 1;
        if (Z != 0)
            sreg |= 2;
        if (N != 0)
            sreg |= 4;
        if (V != 0)
            sreg |= 8;
        if (S != 0)
            sreg |= 16;
        if (H != 0)
            sreg |= 32;
        core.registers.sreg = (byte)(sreg & 0xff);
    }

    private void SBC(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int r = ((opcode >> 5) & 0x10) | (opcode & 0x0f);
        int Rd = core.registers.get(d);
        int Rr = core.registers.get(r);
        int C = core.registers.sreg & 1;
        int res = Rd - Rr - C;
        core.registers.set(d, res & 0xff);
        int Rd7 = (Rd >> 7) & 1;
        int Rr7 = (Rr >> 7) & 1;
        int R7 = (res >> 7) & 1;
        int Rd3 = (Rd >> 3) & 1;
        int Rr3 = (Rr >> 3) & 1;
        int R3 = (res >> 3) & 1;
        int sreg = core.registers.sreg;
        int Z_prev = (sreg & 2) >> 1;
        sreg &= ~0x3f;
        int H = ((1 - Rd3) & Rr3) | (Rr3 & R3) | (R3 & (1 - Rd3));
        int V = (Rd7 & (1 - Rr7) & (1 - R7)) | ((1 - Rd7) & Rr7 & R7);
        int N = R7;
        int S = N ^ V;
        int C_flag = ((1 - Rd7) & Rr7) | (Rr7 & R7) | (R7 & (1 - Rd7));
        int Z = (res & 0xff) == 0 && Z_prev == 1 ? 1 : 0;
        if (C_flag != 0)
            sreg |= 1;
        if (Z != 0)
            sreg |= 2;
        if (N != 0)
            sreg |= 4;
        if (V != 0)
            sreg |= 8;
        if (S != 0)
            sreg |= 16;
        if (H != 0)
            sreg |= 32;
        core.registers.sreg = (byte)(sreg & 0xff);
    }

    private void CP(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int r = ((opcode >> 5) & 0x10) | (opcode & 0x0f);
        int Rd = core.registers.get(d);
        int Rr = core.registers.get(r);
        int res = Rd - Rr;
        int Rd7 = (Rd >> 7) & 1;
        int Rr7 = (Rr >> 7) & 1;
        int R7 = (res >> 7) & 1;
        int Rd3 = (Rd >> 3) & 1;
        int Rr3 = (Rr >> 3) & 1;
        int R3 = (res >> 3) & 1;
        int sreg = core.registers.sreg;
        sreg &= ~0x3f;
        int H = ((1 - Rd3) & Rr3) | (Rr3 & R3) | (R3 & (1 - Rd3));
        int V = (Rd7 & (1 - Rr7) & (1 - R7)) | ((1 - Rd7) & Rr7 & R7);
        int N = R7;
        int S = N ^ V;
        int C_flag = ((1 - Rd7) & Rr7) | (Rr7 & R7) | (R7 & (1 - Rd7));
        int Z = (res & 0xff) == 0 ? 1 : 0;
        if (C_flag != 0)
            sreg |= 1;
        if (Z != 0)
            sreg |= 2;
        if (N != 0)
            sreg |= 4;
        if (V != 0)
            sreg |= 8;
        if (S != 0)
            sreg |= 16;
        if (H != 0)
            sreg |= 32;
        core.registers.sreg = (byte)(sreg & 0xff);
    }

    private void CPC(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int r = ((opcode >> 5) & 0x10) | (opcode & 0x0f);
        int Rd = core.registers.get(d);
        int Rr = core.registers.get(r);
        int C = core.registers.sreg & 1;
        int res = Rd - Rr - C;
        int Rd7 = (Rd >> 7) & 1;
        int Rr7 = (Rr >> 7) & 1;
        int R7 = (res >> 7) & 1;
        int Rd3 = (Rd >> 3) & 1;
        int Rr3 = (Rr >> 3) & 1;
        int R3 = (res >> 3) & 1;
        int sreg = core.registers.sreg;
        int Z_prev = (sreg & 2) >> 1;
        sreg &= ~0x3f;
        int H = ((1 - Rd3) & Rr3) | (Rr3 & R3) | (R3 & (1 - Rd3));
        int V = (Rd7 & (1 - Rr7) & (1 - R7)) | ((1 - Rd7) & Rr7 & R7);
        int N = R7;
        int S = N ^ V;
        int C_flag = ((1 - Rd7) & Rr7) | (Rr7 & R7) | (R7 & (1 - Rd7));
        int Z = (res & 0xff) == 0 && Z_prev == 1 ? 1 : 0;
        if (C_flag != 0)
            sreg |= 1;
        if (Z != 0)
            sreg |= 2;
        if (N != 0)
            sreg |= 4;
        if (V != 0)
            sreg |= 8;
        if (S != 0)
            sreg |= 16;
        if (H != 0)
            sreg |= 32;
        core.registers.sreg = (byte)(sreg & 0xff);
    }

    private void updateLogicFlags(AvrCore core, int res)
    {
        int sreg = core.registers.sreg;
        sreg &= ~0x1e;
        if ((res & 0xff) == 0)
            sreg |= 2;
        if ((res & 0x80) != 0)
            sreg |= 4;
        if ((res & 0x80) != 0)
            sreg |= 16;
        core.registers.sreg = (byte)(sreg & 0xff);
    }

    private void AND(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int r = ((opcode >> 5) & 0x10) | (opcode & 0x0f);
        int res = core.registers.get(d) & core.registers.get(r);
        core.registers.set(d, res);
        this.updateLogicFlags(core, res);
    }

    private void OR(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int r = ((opcode >> 5) & 0x10) | (opcode & 0x0f);
        int res = core.registers.get(d) | core.registers.get(r);
        core.registers.set(d, res);
        this.updateLogicFlags(core, res);
    }

    private void EOR(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int r = ((opcode >> 5) & 0x10) | (opcode & 0x0f);
        int res = core.registers.get(d) ^ core.registers.get(r);
        core.registers.set(d, res);
        this.updateLogicFlags(core, res);
    }

    private void COM(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int res = (~core.registers.get(d)) & 0xff;
        core.registers.set(d, res);
        int sreg = core.registers.sreg;
        sreg &= ~0x1e;
        sreg |= 1;
        if (res == 0)
            sreg |= 2;
        if ((res & 0x80) != 0)
            sreg |= 4;
        if ((res & 0x80) != 0)
            sreg |= 16;
        core.registers.sreg = (byte)(sreg & 0xff);
    }

    private void NEG(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int Rd = core.registers.get(d);
        int res = (0 - Rd) & 0xff;
        core.registers.set(d, res);
        int sreg = core.registers.sreg;
        sreg &= ~0x3f;
        int R3 = (res >> 3) & 1;
        int Rd3 = (Rd >> 3) & 1;
        int H = R3 | Rd3;
        int V = res == 0x80 ? 1 : 0;
        int N = (res >> 7) & 1;
        int S = N ^ V;
        int C_flag = res != 0 ? 1 : 0;
        int Z = res == 0 ? 1 : 0;
        if (C_flag != 0)
            sreg |= 1;
        if (Z != 0)
            sreg |= 2;
        if (N != 0)
            sreg |= 4;
        if (V != 0)
            sreg |= 8;
        if (S != 0)
            sreg |= 16;
        if (H != 0)
            sreg |= 32;
        core.registers.sreg = (byte)(sreg & 0xff);
    }

    private void MOVW(AvrCore core, int opcode)
    {
        int d = ((opcode >> 4) & 0x0f) * 2;
        int r = (opcode & 0x0f) * 2;
        core.registers.set(d, core.registers.get(r));
        core.registers.set(d + 1, core.registers.get(r + 1));
    }

    private int getWord(AvrCore core, int regL)
    {
        return core.registers.get(regL) | (core.registers.get(regL + 1) << 8);
    }

    private void setWord(AvrCore core, int regL, int val)
    {
        core.registers.set(regL, (byte)(val & 0xff));
        core.registers.set(regL + 1, (byte)((val >> 8) & 0xff));
    }

    private int getQ(int opcode)
    {
        return (((opcode & 0x2000) >> 8) | ((opcode & 0x0c00) >> 7) | (opcode & 0x0007));
    }

    private void ST_X(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 26);
        core.memory.write8(addr, (byte)core.registers.get(r));
    }

    private void ST_X_INC(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 26);
        core.memory.write8(addr, (byte)core.registers.get(r));
        this.setWord(core, 26, addr + 1);
    }

    private void ST_X_DEC(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 26) - 1;
        core.memory.write8(addr, (byte)core.registers.get(r));
        this.setWord(core, 26, addr);
    }

    private void LD_X(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 26);
        core.registers.set(d, core.memory.read8(addr));
    }

    private void LD_X_INC(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 26);
        core.registers.set(d, core.memory.read8(addr));
        this.setWord(core, 26, addr + 1);
    }

    private void LD_X_DEC(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 26) - 1;
        core.registers.set(d, core.memory.read8(addr));
        this.setWord(core, 26, addr);
    }

    private void ST_Y_INC(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 28);
        core.memory.write8(addr, (byte)core.registers.get(r));
        this.setWord(core, 28, addr + 1);
    }

    private void ST_Y_DEC(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 28) - 1;
        core.memory.write8(addr, (byte)core.registers.get(r));
        this.setWord(core, 28, addr);
    }

    private void LD_Y_INC(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 28);
        core.registers.set(d, core.memory.read8(addr));
        this.setWord(core, 28, addr + 1);
    }

    private void LD_Y_DEC(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 28) - 1;
        core.registers.set(d, core.memory.read8(addr));
        this.setWord(core, 28, addr);
    }

    private void STD_Y(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int q = this.getQ(opcode);
        int addr = this.getWord(core, 28) + q;
        core.memory.write8(addr, (byte)core.registers.get(r));
    }

    private void LDD_Y(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int q = this.getQ(opcode);
        int addr = this.getWord(core, 28) + q;
        core.registers.set(d, core.memory.read8(addr));
    }

    private void ST_Z_INC(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 30);
        core.memory.write8(addr, (byte)core.registers.get(r));
        this.setWord(core, 30, addr + 1);
    }

    private void ST_Z_DEC(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 30) - 1;
        core.memory.write8(addr, (byte)core.registers.get(r));
        this.setWord(core, 30, addr);
    }

    private void LD_Z_INC(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 30);
        core.registers.set(d, core.memory.read8(addr));
        this.setWord(core, 30, addr + 1);
    }

    private void LD_Z_DEC(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int addr = this.getWord(core, 30) - 1;
        core.registers.set(d, core.memory.read8(addr));
        this.setWord(core, 30, addr);
    }

    private void STD_Z(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int q = this.getQ(opcode);
        int addr = this.getWord(core, 30) + q;
        core.memory.write8(addr, (byte)core.registers.get(r));
    }

    private void LDD_Z(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int q = this.getQ(opcode);
        int addr = this.getWord(core, 30) + q;
        core.registers.set(d, core.memory.read8(addr));
    }

    private void OUT(AvrCore core, int opcode)
    {
        int r = (opcode >> 4) & 0x1f;
        int A = ((opcode >> 5) & 0x30) | (opcode & 0x0f);
        core.memory.write8(A + 32, (byte)(core.registers.get(r) & 0xff));
    }

    private void IN(AvrCore core, int opcode)
    {
        int d = (opcode >> 4) & 0x1f;
        int A = ((opcode >> 5) & 0x30) | (opcode & 0x0f);
        core.registers.set(d, core.memory.read8(A + 32));
    }

    private void SBI(AvrCore core, int opcode)
    {
        int A = (opcode >> 3) & 0x1f;
        int b = opcode & 7;
        int val = core.memory.read8(A + 32);
        core.memory.write8(A + 32, (byte)((val | (1 << b)) & 0xff));
    }

    private void CBI(AvrCore core, int opcode)
    {
        int A = (opcode >> 3) & 0x1f;
        int b = opcode & 7;
        int val = core.memory.read8(A + 32);
        core.memory.write8(A + 32, (byte)((val & ~(1 << b)) & 0xff));
    }

    private void skipNext(AvrCore core)
    {
        int pc = core.registers.pc;
        int low = core.memory.flash[pc];
        int high = core.memory.flash[pc + 1];
        int nextOpcode = (high << 8) | low;
        bool is32Bit =
            (nextOpcode & 0xfe0f) == 0x9000
            || (nextOpcode & 0xfe0f) == 0x9200
            || (nextOpcode & 0xfe0e) == 0x940c
            || (nextOpcode & 0xfe0e) == 0x940e;
        core.registers.pc += is32Bit ? 4 : 2;
    }

    private void SBIS(AvrCore core, int opcode)
    {
        int A = (opcode >> 3) & 0x1f;
        int b = opcode & 7;
        int val = core.memory.read8(A + 32);
        if ((val & (1 << b)) != 0)
        {
            this.skipNext(core);
        }
    }

    private void SBIC(AvrCore core, int opcode)
    {
        int A = (opcode >> 3) & 0x1f;
        int b = opcode & 7;
        int val = core.memory.read8(A + 32);
        if ((val & (1 << b)) == 0)
        {
            this.skipNext(core);
        }
    }
}
