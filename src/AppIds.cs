// The Chroma Broadcast app IDs the bridge can try, in the order it tries them.
//
// Razer's broadcast feed only talks to app IDs on its own allowlist. These 64 IDs are the ones
// published in the open-source OpenChromaConnect headers (inc/RzChromaBroadcastAPIDefines.h,
// commit 1800e64, MIT licence, copyright (c) 2020 Balazs Triszka). They are a compatibility shim
// so the bridge can receive Synapse's colours on PCs where one of them is accepted. They are not
// credentials Razer issued to this project, and Synapse may show the bridge under that brand's name.
//
// Order: the two ASUS-named IDs first (this is an ASUS bridge), then the other partners in header
// order, then Razer's test IDs (which only work with Razer's developer setup), then the sample ID.

using System;
using System.Collections.Generic;

namespace AuraChromaBridge
{
    /// <summary>razer_app_id: off = clock only, auto = try the known IDs, pinned = one ID from settings.ini.</summary>
    internal enum AppIdMode
    {
        Off,
        Auto,
        Pinned
    }

    internal sealed class KnownAppId
    {
        public readonly Guid Id;
        public readonly string Name;

        public KnownAppId(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString()
        {
            return Name + " " + Id.ToString("B");
        }
    }

    internal static class KnownAppIds
    {
        public const string PinnedName = "razer_app_id";

        public static readonly KnownAppId[] All =
        {
            new KnownAppId(new Guid("{DEF05DCE-1662-4D9A-A312-A31028651915}"), "Chroma Control - ASUS"),
            new KnownAppId(new Guid("{4C24AD2B-D0A6-409F-B895-EC7632F3BEE6}"), "ASUS"),
            new KnownAppId(new Guid("{961E436A-C2BC-420E-9884-258D0DB42A87}"), "NZXT CAM"),
            new KnownAppId(new Guid("{EA0ABF8C-807E-45FA-85DE-CC533A3DADA6}"), "Ducky RGB Software"),
            new KnownAppId(new Guid("{368EFB31-B894-41F3-8781-05F10F479519}"), "JBOG Spectrum Server"),
            new KnownAppId(new Guid("{80C92AAF-DD75-4381-8D61-326DFB87711F}"), "AMD"),
            new KnownAppId(new Guid("{4E3B9618-DB98-4E59-B65E-E842C047F7C7}"), "MSI Mystic Light"),
            new KnownAppId(new Guid("{58EB64CE-46F0-441C-B226-3FD3F9DAECB0}"), "Thermaltake"),
            new KnownAppId(new Guid("{891D9CE3-2764-460A-A9BF-7643D4544021}"), "Lian Li"),
            new KnownAppId(new Guid("{D6927055-4E61-48FB-B83F-2227E3E27381}"), "Vertagear"),
            new KnownAppId(new Guid("{5DEA07A2-46E9-468B-A0DA-EA2DF532FCA1}"), "BKSTEC"),
            new KnownAppId(new Guid("{33D8F7B0-FB49-4705-B343-AE536033AFC3}"), "Chroma Control - Corsair"),
            new KnownAppId(new Guid("{04774911-95C2-4F41-8FC6-6A1FC1A683D8}"), "Chroma Control - Gigabyte"),
            new KnownAppId(new Guid("{82A87B65-BB63-4EEC-B79C-2F6EAB053C8A}"), "Title 24"),
            new KnownAppId(new Guid("{367B4411-2E2A-4ABB-9F50-FB07640F28EC}"), "Creative"),
            new KnownAppId(new Guid("{04479F96-EF9C-4C97-866C-7773CC963A86}"), "A4Tech"),
            new KnownAppId(new Guid("{D59DFF8D-E319-48FC-BDF3-04AF2E0148D3}"), "ASROCK"),
            new KnownAppId(new Guid("{3AAA5EC1-084C-4470-BD3C-DCA00F10805F}"), "Biostar"),
            new KnownAppId(new Guid("{60B3EEFD-6A95-4DE7-8541-7F2EC6E89893}"), "Colorful"),
            new KnownAppId(new Guid("{C8DAE01D-961B-44DB-BA87-9454ACD49E02}"), "Cougar"),
            new KnownAppId(new Guid("{230FD850-AD96-478A-9691-1F1D57537437}"), "Primax"),
            new KnownAppId(new Guid("{A4167AD8-48B5-49C4-945B-8F87D55E6FBA}"), "TEAMGROUP"),
            new KnownAppId(new Guid("{C368F5A2-37D7-44CA-85B5-0EBC81D21ED6}"), "ViewSonic"),
            new KnownAppId(new Guid("{08377A3B-7800-4705-9FFF-D1E980DD0FDC}"), "Wooting"),
            new KnownAppId(new Guid("{E0CC986E-3615-420D-B7DD-DC8D90988423}"), "Zotac"),
            new KnownAppId(new Guid("{ED976557-71AA-4785-A398-2710CA981AF8}"), "AOC"),
            new KnownAppId(new Guid("{F4C150EB-7871-41B5-B0B6-B732D3DEA727}"), "ACER"),
            new KnownAppId(new Guid("{533A3A39-E6D6-4C77-B2D6-EFE7B88611FD}"), "Bitspower"),
            new KnownAppId(new Guid("{1093D529-07D3-493C-A2DA-C197F310AAD7}"), "G-SPY"),
            new KnownAppId(new Guid("{92C68BF6-7814-4BFE-9634-3124A42AE2E9}"), "XSplit"),
            new KnownAppId(new Guid("{06737B8F-0F79-4787-89DD-132139E8A92D}"), "XSplit 2"),
            new KnownAppId(new Guid("{776775F5-0E6E-4E19-9D67-F4E25E2DCF0E}"), "THUNDERX3"),
            new KnownAppId(new Guid("{BC335ED7-5EE2-421F-8463-83E84C2EAEFD}"), "XSplit 3"),
            new KnownAppId(new Guid("{1D50582D-C7EB-4B98-B455-C8545695AA31}"), "Ning Mei"),
            new KnownAppId(new Guid("{2716ADD7-EA1C-4A4D-A54C-8AF1A1000A01}"), "PATRIOT VIPER KEYBOARD V770"),
            new KnownAppId(new Guid("{0B9E1BCF-01B6-4F3E-A7A6-292D28E6FDFF}"), "VIPER V570 SOFTWARE"),
            new KnownAppId(new Guid("{EBD1070F-1749-4350-B5CC-54C7AE5315B2}"), "PATRIOT VIPER HEADSET V370"),
            new KnownAppId(new Guid("{BD11684E-A074-46A5-86AF-8B8DA45A509C}"), "daskeyboard"),
            new KnownAppId(new Guid("{F31BCA74-5A17-4D5D-9021-8F73B776D188}"), "GeIL"),
            new KnownAppId(new Guid("{A1988817-8A95-43D6-9FC5-36EB7966144D}"), "Aquatuning"),
            new KnownAppId(new Guid("{C009F863-3664-4DF9-9DA7-F4AAD53BF7B7}"), "LIFX"),
            new KnownAppId(new Guid("{22F3B1AE-241A-40B2-AEDD-10EA03E58F84}"), "Yeelight"),
            new KnownAppId(new Guid("{6660BCDE-7C78-4F45-A40F-31FDCB70624A}"), "G.SKILL"),
            new KnownAppId(new Guid("{E034A449-772D-4790-9E5C-BAC544C0C453}"), "Nike"),
            new KnownAppId(new Guid("{71AC5147-A913-413F-BE92-211ED7BE6CB1}"), "DURGOD"),
            new KnownAppId(new Guid("{CC07B98B-948D-46AE-85BE-90B0E0F33936}"), "Logitech"),
            new KnownAppId(new Guid("{CA707873-DFB3-44B9-96DC-C639AC52872D}"), "GALAX"),
            new KnownAppId(new Guid("{154AA774-E2BF-447B-A48F-92C31E699098}"), "PCCOOLER"),
            new KnownAppId(new Guid("{04FDA7CE-C282-4EE4-AB04-0E66205288FD}"), "Apacer_ZADAK"),
            new KnownAppId(new Guid("{0EED12EE-E807-4546-9F2A-BE9FC1DF5BD6}"), "Tuya"),
            new KnownAppId(new Guid("{51AF0D69-52A4-4E69-A330-F10BED22633C}"), "Wiz"),
            new KnownAppId(new Guid("{FF8B7D7A-B080-4D19-BCB8-1EB9B5919F2D}"), "Zalman"),
            new KnownAppId(new Guid("{9A9730FB-C655-4843-8F31-4C938062E209}"), "XPG"),
            new KnownAppId(new Guid("{9DD054C8-2956-4296-B2FF-023DA9E6F0F9}"), "Chroma Broadcast Test Application 01"),
            new KnownAppId(new Guid("{060F613C-27C6-46A7-B23D-52A4A3CA4458}"), "Chroma Broadcast Test Application 02"),
            new KnownAppId(new Guid("{EAE1BC0C-2508-43BD-B802-2F5CAFDA9FAE}"), "Chroma Broadcast Test Application 03"),
            new KnownAppId(new Guid("{D17290C3-29A2-4F27-BD1B-BD35530C1A56}"), "Chroma Broadcast Test Application 04"),
            new KnownAppId(new Guid("{1C4661C9-541A-4948-A969-AAF3FC085DA2}"), "Chroma Broadcast Test Application 05"),
            new KnownAppId(new Guid("{1F018A4A-B6AB-4438-B3D1-18D29F969AEB}"), "Chroma Broadcast Test Application 06"),
            new KnownAppId(new Guid("{0253BEB5-45F8-464B-84A1-A11E3BD5CD3D}"), "Chroma Broadcast Test Application 07"),
            new KnownAppId(new Guid("{662E9E25-A605-4CDF-8F7A-09F4010B63F8}"), "Chroma Broadcast Test Application 08"),
            new KnownAppId(new Guid("{3C30484A-FE85-454F-903E-6DCE2201DA9C}"), "Chroma Broadcast Test Application 09"),
            new KnownAppId(new Guid("{19F05CAC-FF8D-4F02-B76D-F86346F0A4A9}"), "Chroma Broadcast Test Application 10"),
            new KnownAppId(new Guid("{8FB61199-44F5-4C53-A706-747373A21796}"), "Chroma Broadcast Sample Application")
        };

        /// <summary>The IDs the sync tries for this razer_app_id setting.</summary>
        public static KnownAppId[] Candidates(AppIdMode mode, Guid pinned)
        {
            switch (mode)
            {
                case AppIdMode.Auto:
                    return (KnownAppId[])All.Clone();
                case AppIdMode.Pinned:
                    return new[] { Named(pinned) };
                default:
                    return new KnownAppId[0];
            }
        }

        /// <summary>The IDs the scan tries: every known ID, with a pinned one from settings.ini first.</summary>
        public static KnownAppId[] ForScan(AppIdMode mode, Guid pinned)
        {
            List<KnownAppId> list = new List<KnownAppId>();
            if (mode == AppIdMode.Pinned) list.Add(Named(pinned));
            foreach (KnownAppId id in All)
            {
                if (mode == AppIdMode.Pinned && id.Id == pinned) continue;
                list.Add(id);
            }
            return list.ToArray();
        }

        /// <summary>Tries each ID in order and stops at the first one Synapse accepts (result 0). Null if none.</summary>
        public static KnownAppId FirstAccepted(IList<KnownAppId> candidates, Func<Guid, int> tryInit, Action<KnownAppId, int> report)
        {
            foreach (KnownAppId candidate in candidates)
            {
                int result = tryInit(candidate.Id);
                if (report != null) report(candidate, result);
                if (result == 0) return candidate;
            }
            return null;
        }

        /// <summary>Tries every ID, disconnecting again after each one Synapse accepts. Returns every result, in order.</summary>
        public static List<KeyValuePair<KnownAppId, int>> ProbeAll(IList<KnownAppId> candidates, Func<Guid, int> tryInit, Action uninit)
        {
            List<KeyValuePair<KnownAppId, int>> results = new List<KeyValuePair<KnownAppId, int>>();
            foreach (KnownAppId candidate in candidates)
            {
                int result = tryInit(candidate.Id);
                if (result == 0) uninit();
                results.Add(new KeyValuePair<KnownAppId, int>(candidate, result));
            }
            return results;
        }

        private static KnownAppId Named(Guid id)
        {
            foreach (KnownAppId known in All)
            {
                if (known.Id == id) return known;
            }
            return new KnownAppId(id, PinnedName);
        }
    }
}
