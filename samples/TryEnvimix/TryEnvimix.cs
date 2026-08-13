using ManiaScriptSharp;
using System.Linq;

namespace TryEnvimix;

[NoLoop]
public class TryEnvimix : CMlBrowser, IContext
{
    public void Main()
    {
        if (CurMap is null)
        {
            return;
        }

        var campaignOffset = LoadedTitle.TitleId switch
        {
            "TMCanyon@nadeo" => 0,
            "TMStadium@nadeo" => 65,
            "TMValley@nadeo" => 130,
            "TMLagoon@nadeo" => 195,
            _ => -1
        };

        if (campaignOffset == -1)
        {
            return;
        }

        var mapFound = false;
        var mapNum = 0;

        foreach (var campaign in DataFileMgr.Campaigns)
        {
            foreach (var mapGroup in campaign.MapGroups)
            {
                foreach (var mapInfo in mapGroup.MapInfos)
                {
                    mapNum++;
                    if (mapInfo.MapUid == CurMap.MapInfo.MapUid)
                    {
                        mapFound = true;
                        break;
                    }
                }

                if (mapFound)
                {
                    break;
                }
            }

            if (mapFound)
            {
                break;
            }
        }

        if (!mapFound)
        {
            return;
        }

        ManiaScript.Log($"#campaign=#({campaignOffset} + {mapNum})@Nadeo_Envimix@bigbang1112");
        OpenLink($"#campaign=#{campaignOffset + mapNum}@Nadeo_Envimix@bigbang1112", LinkType.ManialinkBrowser);
    }

    public void Loop()
    {

    }
}
