import struct
import sys
import argparse
from collections import namedtuple
import os
import concurrent.futures
import threading

CRC32_TABLE = [
    0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3,
    0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
    0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE, 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
    0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
    0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172, 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
    0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
    0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F,
    0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924, 0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,
    0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
    0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
    0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457,
    0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
    0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2, 0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB,
    0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0, 0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
    0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
    0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,
    0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683,
    0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8, 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
    0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7,
    0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
    0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
    0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
    0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236, 0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F,
    0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
    0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A, 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
    0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
    0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
    0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C, 0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
    0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB,
    0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
    0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF,
    0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94, 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D,
]

CRC32_SEED = 0xEDB88320

def custom_crc32(data: str, seed=CRC32_SEED):
    crc = seed
    for c in data.encode('utf-8'):
        crc = (crc >> 8) ^ CRC32_TABLE[c ^ (crc & 0xff)]
    return crc & 0xFFFFFFFF

def get_soundbank_hashes():
    names = {
        'kSoundBankBuilt': "Sound Bank Built",
        'kSoundBankInfo': "Sound Bank Info",
        'kSoundBankStrings': "Sound Bank Strings",
        'kSoundBankStreamLookup': "Sound Bank Stream Lookup",
        'kSoundWwiseBnkBuilt': "Sound wwise bnk data",
    }
    return {k: custom_crc32(v) for k, v in names.items()}

DataFileHeader = namedtuple('DataFileHeader', 'file_id version file_size block_count fixup_count')
DataBlockHeader = namedtuple('DataBlockHeader', 'name_hash offset size')

class SoundBankBuilt:
    SIZE = 64
    def __init__(self, data):
        (
            self.wwise_id,         
            self.bank_size,        
            self.bank_name_offset, 
            self.flags,            
            self.pad               
        ) = struct.unpack('<I I H B 53s', data)
    def to_bytes(self):
        return struct.pack('<I I H B 53s', self.wwise_id, self.bank_size, self.bank_name_offset, self.flags, self.pad)

class WwiseInfoElem:
    SIZE = 16

    def __init__(self, data):
        (
            self.name_hash,      
            self.name_offset,    
            self.flags,          
            self.max_attenuation,
            self.min_duration,   
            self.max_duration    
        ) = struct.unpack('<I H H f H H', data)
    def to_bytes(self):
        return struct.pack('<I H H f H H', self.name_hash, self.name_offset, self.flags, self.max_attenuation, self.min_duration, self.max_duration)

class WwiseStreamLookup:
    SIZE = 8
    def __init__(self, data):
        self.file_id, self.event_id = struct.unpack('<II', data)
    def to_bytes(self):
        return struct.pack('<I I', self.wwise_file_id, self.wwise_event_id)

class SoundBank:
    def __init__(self, filename):
        self.filename = filename
        self.blocks = {}
        self.block_headers = []
        self._parse()

    def _parse(self):
        with open(self.filename, 'rb') as f:
            data = f.read()

            dat1_offset = data.find(b'DAT1')
            if dat1_offset == -1:
                dat1_offset = data.find(b'1TAD')
            if dat1_offset == -1:
                raise ValueError(f"'DAT1' or '1TAD' marker not found in file ({self.filename})")

            data = data[dat1_offset:]
            if len(data) < 16:
                raise ValueError(f"File too short or corrupt: cannot read header ({self.filename})")
            self.header = DataFileHeader(*struct.unpack('<I I I H H', data[:16]))

            self.block_headers = []
            offset = 16
            for _ in range(self.header.block_count):
                if offset + 12 > len(data):
                    raise ValueError(f"File too short or corrupt: cannot read block header ({self.filename})")
                bh = DataBlockHeader(*struct.unpack('<I I I', data[offset:offset+12]))
                self.block_headers.append(bh)
                offset += 12

            for bh in self.block_headers:
                if bh.offset + bh.size > len(data):
                    raise ValueError(f"File too short or corrupt: cannot read block data ({self.filename})")
                self.blocks[bh.name_hash] = data[bh.offset:bh.offset+bh.size]

        self._parse_blocks()

    def _parse_blocks(self):

        if K_SOUNDBANK_BUILT_HASH in self.blocks:
            self.bank_built = SoundBankBuilt(self.blocks[K_SOUNDBANK_BUILT_HASH][:SoundBankBuilt.SIZE])
        else:
            self.bank_built = None

        if K_SOUNDBANK_INFO_HASH in self.blocks:
            info_data = self.blocks[K_SOUNDBANK_INFO_HASH]
            self.info_elems = [WwiseInfoElem(info_data[i:i+WwiseInfoElem.SIZE]) for i in range(0, len(info_data), WwiseInfoElem.SIZE)]
        else:
            self.info_elems = []

        if K_SOUNDBANK_STRINGS_HASH in self.blocks:
            self.strings = self.blocks[K_SOUNDBANK_STRINGS_HASH]
        else:
            self.strings = b''

        if K_SOUNDBANK_STREAM_LOOKUP_HASH in self.blocks:
            stream_data = self.blocks[K_SOUNDBANK_STREAM_LOOKUP_HASH]
            self.stream_lookups = [WwiseStreamLookup(stream_data[i:i+WwiseStreamLookup.SIZE]) for i in range(0, len(stream_data), WwiseStreamLookup.SIZE)]
        else:
            self.stream_lookups = []

        if K_SOUNDBWISE_BNK_BUILT_HASH in self.blocks:
            self.wwise_bank_data = self.blocks[K_SOUNDBWISE_BNK_BUILT_HASH]
        else:
            self.wwise_bank_data = b''

        self.eventid_to_fileid = {}
        if K_SOUNDBANK_STREAM_LOOKUP_HASH in self.blocks:
            stream_data = self.blocks[K_SOUNDBANK_STREAM_LOOKUP_HASH]
            for i in range(0, len(stream_data), WwiseStreamLookup.SIZE):
                lookup = WwiseStreamLookup(stream_data[i:i+WwiseStreamLookup.SIZE])
                self.eventid_to_fileid[lookup.event_id] = lookup.file_id

    def compute_wwise_id(self, name):

        return custom_crc32(name)

    def list_events(self):

        print(f"{'Idx':<4} {'Event Name':<40} {'Hash':<10} {'Flags':<6} {'MaxAttn':<8} {'MinDur':<6} {'MaxDur':<6} {'WEM File':<16}")
        print('-'*100)
        for idx, elem in enumerate(self.info_elems):
            name = self.get_string(elem.name_offset)

            wem_file = ''
            file_id = self.eventid_to_fileid.get(elem.name_hash)
            if file_id is not None:
                wem_file = f"{file_id:08x}.wem"
            print(f"{idx:<4} {name:<40} {elem.name_hash:08x} {elem.flags:<6} {elem.max_attenuation:<8.1f} {elem.min_duration:<6} {elem.max_duration:<6} {wem_file:<16}")

    def get_string(self, offset):

        real_offset = (offset << 2)
        end = self.strings.find(b'\x00', real_offset)
        return self.strings[real_offset:end].decode('utf-8') if end != -1 else ''

HASHES = get_soundbank_hashes()
K_SOUNDBANK_BUILT_HASH = HASHES['kSoundBankBuilt']
K_SOUNDBANK_INFO_HASH = HASHES['kSoundBankInfo']
K_SOUNDBANK_STRINGS_HASH = HASHES['kSoundBankStrings']
K_SOUNDBANK_STREAM_LOOKUP_HASH = HASHES['kSoundBankStreamLookup']
K_SOUNDBWISE_BNK_BUILT_HASH = HASHES['kSoundWwiseBnkBuilt']

def main():
    parser = argparse.ArgumentParser(description='Read/Edit .soundbank files')
    parser.add_argument('file', nargs='?', help='.soundbank file to read')
    parser.add_argument('--list', action='store_true', help='List events')
    parser.add_argument('--set-event-name', nargs=2, metavar=('INDEX', 'NAME'), help='Set event name (demo only)')
    parser.add_argument('--save', metavar='OUTFILE', help='Save modified file (demo only)')
    parser.add_argument('--print-hashes', action='store_true', help='Print block name hashes')
    parser.add_argument('--wem-to-event', metavar='WEM_ID', type=int, help='Given a .wem file number, print all event names that map to it')
    parser.add_argument('--list-wems', action='store_true', help='List all .wem file numbers and the event names that use them')
    parser.add_argument('--folder', metavar='DIR', help='Process all .soundbank files in this directory (for --list-wems only)')
    parser.add_argument('--out', metavar='OUTFILE', help='Write output to this file (for --folder + --list-wems)')
    args = parser.parse_args()

    if args.print_hashes:
        for k, v in HASHES.items():
            print(f"{k}: 0x{v:08X}")
        return

    def list_wems_for_bank(sb, bank_name):
        lines = []
        wem_to_event = {}
        for elem in sb.info_elems:
            event_name = sb.get_string(elem.name_offset)
            file_id = sb.eventid_to_fileid.get(elem.name_hash)
            if file_id is not None:
                wem_to_event.setdefault(file_id, []).append(event_name)
        lines.append(f"==== {bank_name} ====")
        lines.append(f"{'WEM File':<12} | Event Name(s)")
        lines.append('-'*60)
        for wem_id, event_names in sorted(wem_to_event.items()):
            wem_file = f"{wem_id}.wem"
            lines.append(f"{wem_file:<12} | {', '.join(event_names)}")
        lines.append("")
        return lines, len(wem_to_event)

    if args.folder and args.list_wems:
        soundbank_files = sorted([fname for fname in os.listdir(args.folder) if fname.endswith('.soundbank')])
        total_files = len(soundbank_files)
        out_lines = []
        wem_total = 0
        lock = threading.Lock()

        def process_file(fname):
            full_path = os.path.join(args.folder, fname)
            try:
                sb = SoundBank(full_path)
                lines, wem_count = list_wems_for_bank(sb, fname)
                with lock:
                    nonlocal wem_total
                    wem_total += wem_count
                    print(f"Processed: {fname} ({wem_count} wem files, {wem_total} total so far)")
                return lines
            except Exception as e:
                with lock:
                    print(f"Error reading {fname}: {e}")
                return [f"==== {fname} ====", f"Error reading soundbank: {e}", ""]

        with concurrent.futures.ThreadPoolExecutor() as executor:
            results = list(executor.map(process_file, soundbank_files))

        for lines in results:
            out_lines.extend(lines)

        if args.out:
            with open(args.out, 'w', encoding='utf-8') as f:
                f.write('\n'.join(out_lines))
            print(f"Wrote output to {args.out}")
        else:
            print('\n'.join(out_lines))
        return

    if not args.file:
        parser.print_help()
        return

    sb = SoundBank(args.file)
    if args.list:
        sb.list_events()
    if args.set_event_name:
        idx, name = int(args.set_event_name[0]), args.set_event_name[1]
        sb.set_event_name(idx, name)
    if args.save:
        sb.save(args.save)
    if args.wem_to_event is not None:
        wem_id = args.wem_to_event

        wem_to_event = {}
        for elem in sb.info_elems:
            event_name = sb.get_string(elem.name_offset)
            file_id = sb.eventid_to_fileid.get(elem.name_hash)
            if file_id is not None:
                wem_to_event.setdefault(file_id, []).append(event_name)
        event_names = wem_to_event.get(wem_id, [])
        if event_names:
            print(f"Event name(s) for {wem_id}.wem:")
            for name in event_names:
                print(f"  {name}")
        else:
            print(f"No event name found for {wem_id}.wem in this soundbank.")
    if args.list_wems and not args.folder:
        lines = list_wems_for_bank(sb, args.file)
        print('\n'.join(lines))

if __name__ == '__main__':
    main()