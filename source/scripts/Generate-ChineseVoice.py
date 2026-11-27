"""Author the optional Mandarin navigation clips; playback is entirely local.

Requires edge-tts in the authoring environment. Never run by builds or the app.
Existing clips are kept; delete a clip to regenerate it.
The bundled clips were then trimmed to 50 ms lead-in / 120 ms tail so a distance
and its action play back to back:
  ffmpeg -i in.mp3 -af "silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.05,areverse,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.12,areverse" -map_metadata -1 -ac 1 -ar 24000 -c:a libmp3lame -b:a 64k -id3v2_version 0 -write_id3v1 0 out.mp3
No ID3 tag: Windows MCI (mpegvideo) cannot open clips carrying edge-tts's C2PA ID3v2.4 frame.
"""
import argparse
import asyncio
import sys
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument('--dependency-path')
parser.add_argument('--voice',default='zh-CN-XiaoxiaoNeural')
args=parser.parse_args()
if args.dependency_path:sys.path.insert(0,args.dependency_path)
import edge_tts
ROOT=Path(__file__).resolve().parents[1]
NAME='晓晓 (中文)'
# 11_in.mp3 is not used: each distance clip already says 前方, so a cue plays
# "前方五百米" followed by the action, e.g. "左转".
CLIPS={
    '01_500_meters.mp3':'前方五百米',
    '02_250_meters.mp3':'前方二百五十米',
    '03_100_meters.mp3':'前方一百米',
    '04_50_meters.mp3':'前方五十米',
    '05_continue_straight.mp3':'继续直行',
    '06_turn_left.mp3':'左转',
    '07_turn_right.mp3':'右转',
    '08_keep_left.mp3':'靠左行驶',
    '09_keep_right.mp3':'靠右行驶',
    '10_make_a_u_turn.mp3':'请掉头',
    '12_you_have_arrived.mp3':'您已到达目的地',
    '13_recalculating.mp3':'正在重新规划路线',
}
async def main():
    voices={entry['ShortName'] for entry in await edge_tts.list_voices()}
    assert args.voice in voices,args.voice
    folder=ROOT/'resources/voice-navigation'/NAME
    folder.mkdir(parents=True,exist_ok=True)
    for file,text in CLIPS.items():
        output=folder/file
        if not output.exists():await edge_tts.Communicate(text,args.voice,rate='-5%').save(str(output))
        assert output.stat().st_size>1000,file
        print(args.voice,file,text,output.stat().st_size,flush=True)
asyncio.run(main())
