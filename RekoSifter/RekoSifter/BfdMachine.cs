using System;

namespace RekoSifter
{
	[Flags]
	public enum BfdMachine : uint
	{
		#region m68k      /* Motorola 68xxx.  */
		m68000 = 1,
		m68008 = 2,
		m68010 = 3,
		m68020 = 4,
		m68030 = 5,
		m68040 = 6,
		m68060 = 7,
		cpu32 = 8,
		fido = 9,
		mcf_isa_a_nodiv = 10,
		mcf_isa_a = 11,
		mcf_isa_a_mac = 12,
		mcf_isa_a_emac = 13,
		mcf_isa_aplus = 14,
		mcf_isa_aplus_mac = 15,
		mcf_isa_aplus_emac = 16,
		mcf_isa_b_nousp = 17,
		mcf_isa_b_nousp_mac = 18,
		mcf_isa_b_nousp_emac = 19,
		mcf_isa_b = 20,
		mcf_isa_b_mac = 21,
		mcf_isa_b_emac = 22,
		mcf_isa_b_float = 23,
		mcf_isa_b_float_mac = 24,
		mcf_isa_b_float_emac = 25,
		mcf_isa_c = 26,
		mcf_isa_c_mac = 27,
		mcf_isa_c_emac = 28,
		mcf_isa_c_nodiv = 29,
		mcf_isa_c_nodiv_mac = 30,
		mcf_isa_c_nodiv_emac = 31,
		#endregion
		#region or1k      /* OpenRISC 1000.  */
		or1k = 1,
		or1knd = 2,
		#endregion
		#region sparc     /* SPARC.  */
		sparc = 1,
		sparc_sparclet = 2,
		sparc_sparclite = 3,
		sparc_v8plus = 4,
		sparc_v8plusa = 5 /* with ultrasparc add'ns.  */,
		sparc_sparclite_le = 6,
		sparc_v9 = 7,
		sparc_v9a = 8 /* with ultrasparc add'ns.  */,
		sparc_v8plusb = 9 /* with cheetah add'ns.  */,
		sparc_v9b = 10 /* with cheetah add'ns.  */,
		sparc_v8plusc = 11 /* with UA2005 and T1 add'ns.  */,
		sparc_v9c = 12 /* with UA2005 and T1 add'ns.  */,
		sparc_v8plusd = 13 /* with UA2007 and T3 add'ns.  */,
		sparc_v9d = 14 /* with UA2007 and T3 add'ns.  */,
		sparc_v8pluse = 15 /* with OSA2001 and T4 add'ns (no IMA).  */,
		sparc_v9e = 16 /* with OSA2001 and T4 add'ns (no IMA).  */,
		sparc_v8plusv = 17 /* with OSA2011 and T4 and IMA and FJMAU add'ns.  */,
		sparc_v9v = 18 /* with OSA2011 and T4 and IMA and FJMAU add'ns.  */,
		sparc_v8plusm = 19 /* with OSA2015 and M7 add'ns.  */,
		sparc_v9m = 20 /* with OSA2015 and M7 add'ns.  */,
		sparc_v8plusm8 = 21 /* with OSA2017 and M8 add'ns.  */,
		sparc_v9m8 = 22 /* with OSA2017 and M8 add'ns.  */,
		#endregion
		#region spu       /* PowerPC SPU.  */
		spu = 256,
		#endregion
		#region mips      /* MIPS Rxxxx.  */
		mips3000 = 3000,
		mips3900 = 3900,
		mips4000 = 4000,
		mips4010 = 4010,
		mips4100 = 4100,
		mips4111 = 4111,
		mips4120 = 4120,
		mips4300 = 4300,
		mips4400 = 4400,
		mips4600 = 4600,
		mips4650 = 4650,
		mips5000 = 5000,
		mips5400 = 5400,
		mips5500 = 5500,
		mips5900 = 5900,
		mips6000 = 6000,
		mips7000 = 7000,
		mips8000 = 8000,
		mips9000 = 9000,
		mips10000 = 10000,
		mips12000 = 12000,
		mips14000 = 14000,
		mips16000 = 16000,
		mips16 = 16,
		mips5 = 5,
		mips_loongson_2e = 3001,
		mips_loongson_2f = 3002,
		mips_gs464 = 3003,
		mips_gs464e = 3004,
		mips_gs264e = 3005,
		mips_sb1 = 12310201 /* octal 'SB', 01.  */,
		mips_octeon = 6501,
		mips_octeonp = 6601,
		mips_octeon2 = 6502,
		mips_octeon3 = 6503,
		mips_xlr = 887682   /* decimal 'XLR'.  */,
		mips_interaptiv_mr2 = 736550   /* decimal 'IA2'.  */,
		mipsisa32 = 32,
		mipsisa32r2 = 33,
		mipsisa32r3 = 34,
		mipsisa32r5 = 36,
		mipsisa32r6 = 37,
		mipsisa64 = 64,
		mipsisa64r2 = 65,
		mipsisa64r3 = 66,
		mipsisa64r5 = 68,
		mipsisa64r6 = 69,
		mips_micromips = 96,
		#endregion
		#region i386      /* Intel 386.  */
		i386_intel_syntax = (1 << 0),
		i386_i8086 = (1 << 1),
		i386_i386 = (1 << 2),
		x86_64 = (1 << 3),
		x64_32 = (1 << 4),
		i386_i386_intel_syntax = (i386_i386 | i386_intel_syntax),
		x86_64_intel_syntax = (x86_64 | i386_intel_syntax),
		x64_32_intel_syntax = (x64_32 | i386_intel_syntax),
		#endregion
		#region l1om      /* Intel L1OM.  */
		l1om = (1 << 5),
		l1om_intel_syntax = (l1om | i386_intel_syntax),
		#endregion
		#region k1om      /* Intel K1OM.  */
		k1om = (1 << 6),
		k1om_intel_syntax = (k1om | i386_intel_syntax),
		i386_nacl = (1 << 7),
		i386_i386_nacl = (i386_i386 | i386_nacl),
		x86_64_nacl = (x86_64 | i386_nacl),
		x64_32_nacl = (x64_32 | i386_nacl),
		#endregion
		#region iamcu     /* Intel MCU.  */
		iamcu = (1 << 8),
		i386_iamcu = (i386_i386 | iamcu),
		i386_iamcu_intel_syntax = (i386_iamcu | i386_intel_syntax),
		#endregion
		#region h8300     /* Renesas H8/300 (formerly Hitachi H8/300).  */
		h8300 = 1,
		h8300h = 2,
		h8300s = 3,
		h8300hn = 4,
		h8300sn = 5,
		h8300sx = 6,
		h8300sxn = 7,
		#endregion
		#region powerpc   /* PowerPC.  */
		ppc = 32,
		ppc64 = 64,
		ppc_403 = 403,
		ppc_403gc = 4030,
		ppc_405 = 405,
		ppc_505 = 505,
		ppc_601 = 601,
		ppc_602 = 602,
		ppc_603 = 603,
		ppc_ec603e = 6031,
		ppc_604 = 604,
		ppc_620 = 620,
		ppc_630 = 630,
		ppc_750 = 750,
		ppc_860 = 860,
		ppc_a35 = 35,
		ppc_rs64ii = 642,
		ppc_rs64iii = 643,
		ppc_7400 = 7400,
		ppc_e500 = 500,
		ppc_e500mc = 5001,
		ppc_e500mc64 = 5005,
		ppc_e5500 = 5006,
		ppc_e6500 = 5007,
		ppc_titan = 83,
		ppc_vle = 84,
		#endregion
		#region rs6000    /* IBM RS/6000.  */
		rs6k = 6000,
		rs6k_rs1 = 6001,
		rs6k_rsc = 6003,
		rs6k_rs2 = 6002,
		#endregion
		#region hppa      /* HP PA RISC.  */
		hppa10 = 10,
		hppa11 = 11,
		hppa20 = 20,
		hppa20w = 25,
		#endregion
		#region d10v      /* Mitsubishi D10V.  */
		d10v = 1,
		d10v_ts2 = 2,
		d10v_ts3 = 3,
		#endregion
		#region m68hc12   /* Motorola 68HC12.  */
		m6812_default = 0,
		m6812 = 1,
		m6812s = 2,
		#endregion
		#region s12z    /* Freescale S12Z.  */
		s12z_default = 0,
		#endregion
		#region z8k       /* Zilog Z8000.  */
		z8001 = 1,
		z8002 = 2,
		#endregion
		#region sh        /* Renesas / SuperH SH (formerly Hitachi SH).  */
		sh = 1,
		sh2 = 0x20,
		sh_dsp = 0x2d,
		sh2a = 0x2a,
		sh2a_nofpu = 0x2b,
		sh2a_nofpu_or_sh4_nommu_nofpu = 0x2a1,
		sh2a_nofpu_or_sh3_nommu = 0x2a2,
		sh2a_or_sh4 = 0x2a3,
		sh2a_or_sh3e = 0x2a4,
		sh2e = 0x2e,
		sh3 = 0x30,
		sh3_nommu = 0x31,
		sh3_dsp = 0x3d,
		sh3e = 0x3e,
		sh4 = 0x40,
		sh4_nofpu = 0x41,
		sh4_nommu_nofpu = 0x42,
		sh4a = 0x4a,
		sh4a_nofpu = 0x4b,
		sh4al_dsp = 0x4d,
		#endregion
		#region alpha     /* Dec Alpha.  */
		alpha_ev4 = 0x10,
		alpha_ev5 = 0x20,
		alpha_ev6 = 0x30,
		#endregion
		#region arm       /* Advanced Risc Machines ARM.  */
		arm_unknown = 0,
		arm_2 = 1,
		arm_2a = 2,
		arm_3 = 3,
		arm_3M = 4,
		arm_4 = 5,
		arm_4T = 6,
		arm_5 = 7,
		arm_5T = 8,
		arm_5TE = 9,
		arm_XScale = 10,
		arm_ep9312 = 11,
		arm_iWMMXt = 12,
		arm_iWMMXt2 = 13,
		arm_5TEJ = 14,
		arm_6 = 15,
		arm_6KZ = 16,
		arm_6T2 = 17,
		arm_6K = 18,
		arm_7 = 19,
		arm_6M = 20,
		arm_6SM = 21,
		arm_7EM = 22,
		arm_8 = 23,
		arm_8R = 24,
		arm_8M_BASE = 25,
		arm_8M_MAIN = 26,
		arm_8_1M_MAIN = 27,
		#endregion
		#region nds32     /* Andes NDS32.  */
		n1 = 1,
		n1h = 2,
		n1h_v2 = 3,
		n1h_v3 = 4,
		n1h_v3m = 5,
		#endregion
		#region tic4x     /* Texas Instruments TMS320C3X/4X.  */
		tic3x = 30,
		tic4x = 40,
		#endregion
		#region v850_rh850/* NEC V850 (using RH850 ABI).  */
		v850 = 1,
		v850e = 'E',
		v850e1 = '1',
		v850e2 = 0x4532,
		v850e2v3 = 0x45325633,
		v850e3v5 = 0x45335635 /* ('E'|'3'|'V'|'5').  */,
		#endregion
		#region arc       /* ARC Cores.  */
		arc_a4 = 0,
		arc_a5 = 1,
		arc_arc600 = 2,
		arc_arc601 = 4,
		arc_arc700 = 3,
		arc_arcv2 = 5,
		#endregion
		#region m32c       /* Renesas M16C/M32C.  */
		m16c = 0x75,
		m32c = 0x78,
		#endregion
		#region m32r      /* Renesas M32R (formerly Mitsubishi M32R/D).  */
		m32r = 1 /* For backwards compatibility.  */,
		m32rx = 'x',
		m32r2 = '2',
		#endregion
		#region mn10300   /* Matsushita MN10300.  */
		mn10300 = 300,
		am33 = 330,
		am33_2 = 332,
		#endregion
		#region fr30
		fr30 = 0x46523330,
		#endregion
		#region frv
		frv = 1,
		frvsimple = 2,
		fr300 = 300,
		fr400 = 400,
		fr450 = 450,
		frvtomcat = 499     /* fr500 prototype.  */,
		fr500 = 500,
		fr550 = 550,
		#endregion
		#region moxie     /* The moxie processor.  */
		moxie = 1,
		#endregion
		#region ft32      /* The ft32 processor.  */
		ft32 = 1,
		ft32b = 2,
		#endregion
		#region mep
		mep = 1,
		mep_h1 = 0x6831,
		mep_c5 = 0x6335,
		#endregion
		#region metag
		metag = 1,
		#endregion
		#region ia64      /* HP/Intel ia64.  */
		ia64_elf64 = 64,
		ia64_elf32 = 32,
		#endregion
		#region ip2k      /* Ubicom IP2K microcontrollers. */
		ip2022 = 1,
		ip2022ext = 2,
		#endregion
		#region iq2000     /* Vitesse IQ2000.  */
		iq2000 = 1,
		iq10 = 2,
		#endregion
		#region bpf       /* Linux eBPF.  */
		bpf = 1,
		#endregion
		#region epiphany  /* Adapteva EPIPHANY.  */
		epiphany16 = 1,
		epiphany32 = 2,
		#endregion
		#region mt
		ms1 = 1,
		mrisc2 = 2,
		ms2 = 3,
		#endregion
		#region avr       /* Atmel AVR microcontrollers.  */
		avr1 = 1,
		avr2 = 2,
		avr25 = 25,
		avr3 = 3,
		avr31 = 31,
		avr35 = 35,
		avr4 = 4,
		avr5 = 5,
		avr51 = 51,
		avr6 = 6,
		avrtiny = 100,
		avrxmega1 = 101,
		avrxmega2 = 102,
		avrxmega3 = 103,
		avrxmega4 = 104,
		avrxmega5 = 105,
		avrxmega6 = 106,
		avrxmega7 = 107,
		#endregion
		#region bfin      /* ADI Blackfin.  */
		bfin = 1,
		#endregion
		#region cr16      /* National Semiconductor CompactRISC (ie CR16).  */
		cr16 = 1,
		#endregion
		#region crx       /*  National Semiconductor CRX.  */
		crx = 1,
		#endregion
		#region cris      /* Axis CRIS.  */
		cris_v0_v10 = 255,
		cris_v32 = 32,
		cris_v10_v32 = 1032,
		#endregion
		#region riscv
		riscv32 = 132,
		riscv64 = 164,
		#endregion
		#region rl78
		rl78 = 0x75,
		#endregion
		#region rx        /* Renesas RX.  */
		rx = 0x75,
		rx_v2 = 0x76,
		rx_v3 = 0x77,
		#endregion
		#region s390      /* IBM s390.  */
		s390_31 = 31,
		s390_64 = 64,
		#endregion
		#region score     /* Sunplus score.  */
		score3 = 3,
		score7 = 7,
		#endregion
		#region xstormy16
		xstormy16 = 1,
		#endregion
		#region msp430    /* Texas Instruments MSP430 architecture.  */
		msp11 = 11,
		msp110 = 110,
		msp12 = 12,
		msp13 = 13,
		msp14 = 14,
		msp15 = 15,
		msp16 = 16,
		msp20 = 20,
		msp21 = 21,
		msp22 = 22,
		msp23 = 23,
		msp24 = 24,
		msp26 = 26,
		msp31 = 31,
		msp32 = 32,
		msp33 = 33,
		msp41 = 41,
		msp42 = 42,
		msp43 = 43,
		msp44 = 44,
		msp430x = 45,
		msp46 = 46,
		msp47 = 47,
		msp54 = 54,
		#endregion
		#region xc16x     /* Infineon's XC16X Series.  */
		xc16x = 1,
		xc16xl = 2,
		xc16xs = 3,
		#endregion
		#region xgate     /* Freescale XGATE.  */
		xgate = 1,
		#endregion
		#region xtensa    /* Tensilica's Xtensa cores.  */
		xtensa = 1,
		#endregion
		#region z80
		gbz80 = 0 /* GameBoy Z80 (reduced instruction set) */,
		z80strict = 1 /* Z80 without undocumented opcodes.  */,
		z180 = 2 /* Z180: successor with additional instructions, but without halves of ix and iy */,
		z80 = 3 /* Z80 with ixl, ixh, iyl, and iyh.  */,
		ez80_z80 = 4 /* eZ80 (successor of Z80 & Z180) in Z80 (16-bit address) mode */,
		ez80_adl = 5 /* eZ80 (successor of Z80 & Z180) in ADL (24-bit address) mode */,
		z80full = 7 /* Z80 with all undocumented instructions.  */,
		r800 = 11 /* R800: successor with multiplication.  */,
		#endregion
		#region lm32      /* Lattice Mico32.  */
		lm32 = 1,
		#endregion
		#region tilegx    /* Tilera TILE-Gx.  */
		tilepro = 1,
		tilegx = 1,
		tilegx32 = 2,
		#endregion
		#region aarch64   /* AArch64.  */
		aarch64 = 0,
		aarch64_ilp32 = 32,
		#endregion
		#region nios2     /* Nios II.  */
		nios2 = 0,
		nios2r1 = 1,
		nios2r2 = 2,
		#endregion
		#region visium    /* Visium.  */
		visium = 1,
		#endregion
		#region wasm32    /* WebAssembly.  */
		wasm32 = 1,
		#endregion
		#region pru       /* PRU.  */
		pru = 0,
		#endregion
		#region nfp       /* Netronome Flow Processor */
		nfp3200 = 0x3200,
		nfp6000 = 0x6000,
		#endregion
		#region csky      /* C-SKY.  */
		ck_unknown = 0,
		ck510 = 1,
		ck610 = 2,
		ck801 = 3,
		ck802 = 4,
		ck803 = 5,
		ck807 = 6,
		ck810 = 7,
		#endregion
	}

}