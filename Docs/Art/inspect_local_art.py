"""Read-only inventory of installed reference art; never imports into Unity."""
import json
import zipfile
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
ARCHIVE = Path(r'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Assets.zip')
SOURCE = Path(r'C:\Users\kijin\Downloads\Ludeon public art assets\Game art source\Game art source - RimWorld')

if __name__ == '__main__':
    samples = {
        'body-front': 'Things/Pawn/Humanlike/Bodies/Naked_Male_south.png.dds.split',
        'body-back': 'Things/Pawn/Humanlike/Bodies/Naked_Male_north.png.dds.split',
        'body-side': 'Things/Pawn/Humanlike/Bodies/Naked_Male_east.png.dds.split',
        'head-front': 'Things/Pawn/Humanlike/Heads/Male/Male_Average_Normal_south.png.dds.split',
        'bed': 'Things/Building/Furniture/Bed/Bed_south.psd.dds.split',
        'stove': 'Things/Building/Production/TableStoveFueled_south.psd.dds.split',
        'door': 'Things/Building/Door/DoorSimple_Mover.psd.dds.split',
        'wall-atlas': 'Things/Building/Linked/Wall/Wall_Atlas_Planks.psd.dds.split',
        'tree': 'Things/Plant/TreeOak/TreeOakA.psd.dds.split',
        'gather-plant': 'Things/Plant/BerryPlant/BerryBushA.psd.dds.split',
        'crop-mature': 'Things/Plant/RicePlant/RicePlantA.psd.dds.split',
        'crop-young': 'Things/Plant/RicePlant_Immature/RicePlantImmature.png.dds.split',
        'wood': 'Things/Item/Resource/WoodLog/WoodLog_a.psd.dds.split',
        'ingredients': 'Things/Item/Resource/PlantFoodRaw/Rice.psd.dds.split',
        'meal': 'Things/Item/Meal/Simple/Simple_a.png.dds.split',
        'soil': 'Terrain/Surfaces/Soil.psd.dds.split',
        'soil-rich': 'Terrain/Surfaces/SoilRich.psd.dds.split',
        'button': 'UI/Widgets/ButtonBG.psd.dds.split',
        'draft': 'UI/Commands/Draft.png.dds.split',
        'close': 'UI/Widgets/CloseX.psd.dds.split',
    }
    out = ROOT / 'local-reference'
    out.mkdir(exist_ok=True)
    records = []
    with zipfile.ZipFile(ARCHIVE) as z:
        textures = [i for i in z.infolist() if i.filename.startswith('Resources/Textures/')]
        for key, suffix in samples.items():
            info = z.getinfo('Resources/Textures/' + suffix)
            source = SOURCE / suffix.removesuffix('.dds.split')
            header_image = Image.open(source)
            size = header_image.size
            error = None
            bounds = None
            try:
                im = header_image.convert('RGBA')
                im.load()
                bounds = im.getchannel('A').getbbox()
                im.thumbnail((160, 160), Image.Resampling.LANCZOS)
                im.save(out / (key + '.png'))
            except OSError as exc:
                error = str(exc)
            records.append(dict(key=key, source=str(source), archive=str(ARCHIVE), entry=info.filename,
                                bytes=info.file_size, crc32=f'{info.CRC:08x}', width=size[0], height=size[1],
                                alpha_bounds=bounds, preview=None if error else 'local-reference/' + key + '.png',
                                visual_review='PSD composite has abnormal colors; needs layer-aware export' if key in {'bed','wood','ingredients'} else 'Contact sheet visually inspected 2026-11-17',
                                status=('Decode failed: ' + error) if error else 'PNG/PSD composite read; reference only, not imported into game'))
        archive_count = len(z.infolist())
    manifest = dict(date='2026-11-17', archive=str(ARCHIVE), archive_entries=archive_count,
                    texture_entries=len(textures), samples=records,
                    source_root=str(SOURCE), scope='Current Unity Assets, E:/Dev/RimworldTweak, Steam installation and Downloads public art folder. Not a whole-disk search.',
                    archive_decode_note='Direct DDS split decoding yielded visibly corrupt images and one truncation error. Discarded all such previews; current previews come from Downloads PNG/PSD originals.')
    (ROOT / 'local-art-inventory.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    sheet = Image.new('RGB', (1000, 890), '#e9e7df')
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 16)
    draw.text((18, 12), 'LOCAL RIMWORLD REFERENCES - not our generated art or a game capture', fill='#222222', font=font)
    for idx, row in enumerate(records):
        x, y = 10 + (idx % 5) * 198, 50 + (idx // 5) * 207
        draw.rectangle((x,y,x+184,y+165), fill='#a9b0b1')
        if row['preview']:
            im = Image.open(ROOT / row['preview']).convert('RGBA')
            sheet.paste(im, (x+(184-im.width)//2,y+(165-im.height)//2), im)
            if row['key'] in {'bed', 'wood', 'ingredients'}:
                draw.rectangle((x,y,x+184,y+22), fill='#f4ce9a')
                draw.text((x+4,y+2), 'PSD composite issue', fill='#222222', font=font)
        else:
            draw.text((x+10,y+60), 'Decode unavailable', fill='#222222', font=font)
        draw.text((x,y+169), row['key'], fill='#222222', font=font)
        draw.text((x,y+187), f"{row['width']} x {row['height']}", fill='#444444', font=font)
    sheet.save(ROOT / 'local-reference-contact-sheet.png')
    print(json.dumps(dict(textures=len(textures), decoded=sum(bool(r['preview']) for r in records), samples=[(r['key'],r['width'],r['height'],bool(r['preview'])) for r in records])))
