using ManiaScriptSharp;
using System.Linq;

namespace TryEnvimix;

[NoLoop]
public class TryEnvimix : CMlBrowser, IContext
{
    public void Main()
    {
        var campaignMultiplier = LoadedTitle.TitleId switch
        {
            "TMUF" => 1.0f,
            "TM2" => 1.5f,
            "TMS" => 2.0f,
            _ => 1.0f
        };

        var mapNum = -1;

        foreach (var (i, campaign) in DataFileMgr.Campaigns.Index())
        {
            foreach (var (j, mapGroup) in campaign.MapGroups.Index())
            {
                foreach (var (k, map) in mapGroup.MapInfos.Index())
                {
                    if (map.MapUid == CurMap.MapInfo.MapUid)
                    {
                        mapNum++;
                        break;
                    }
                }
            }
        }
    }

    public void Loop()
    {

    }
}
