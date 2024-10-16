﻿using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSSellHousePacket : GamePacket
{
    public CSSellHousePacket() : base(CSOffsets.CSSellHousePacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        var tl = stream.ReadUInt16();
        var moneyAmount = stream.ReadUInt32();
        var sellTo = stream.ReadString();
        var isPublic = stream.ReadBoolean();
        Logger.Debug($"SellHouse, Tl: {tl}, MoneyAmount: {moneyAmount}, SellTo: {sellTo}, isPublic: {isPublic}");

        // Get buyer Id
        var sellToId = 0u;
        if (!string.IsNullOrEmpty(sellTo))
        {
            sellToId = NameManager.Instance.GetCharacterId(sellTo);
            if (sellToId <= 0)
            {
                // Invalid buyer specified
                Connection.ActiveChar.SendErrorMessage(ErrorMessageType.HouseCannotSellAsDesignatedBuyerNotFound);
                return;
            }
        }

        if (moneyAmount > 0)
            HousingManager.Instance.SetForSale(tl, moneyAmount, sellToId, Connection.ActiveChar);
        else
            HousingManager.Instance.CancelForSale(tl, true);
    }
}
