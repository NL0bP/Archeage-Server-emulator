﻿using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Auction;
using AAEmu.Game.Models.Game.Auction.Templates;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Utils.DB;

using MySql.Data.MySqlClient;

using NLog;

namespace AAEmu.Game.Core.Managers
{
    public class AuctionManager : Singleton<AuctionManager>
    {
        protected static Logger _log = LogManager.GetCurrentClassLogger();

        public List<AuctionItem> _auctionItems;
        public Dictionary<uint, string> _en_localizations;
        public List<long> _deletedAuctionItemIds;

        public void ListAuctionItem(Character player, ulong itemId, uint startPrice, uint buyoutPrice, byte duration)
        {
            var newItem = player.Inventory.GetItemById(itemId);
            var newAuctionItem = CreateAuctionItem(player, newItem, startPrice, buyoutPrice, duration);

            if (newAuctionItem == null) //TODO
            {
                return;
            }

            if (newItem == null) //TODO
            {
                return;
            }

            var auctionFee = (newAuctionItem.DirectMoney * .01) * (duration + 1);

            if (auctionFee > 1000000)//100 gold max fee
            {
                auctionFee = 1000000;
            }

            if (!player.ChangeMoney(SlotType.Inventory, -(int)auctionFee))
            {
                player.SendErrorMessage(ErrorMessageType.CanNotPutupMoney);
                return;
            }
            player.Inventory.Bag.RemoveItem(Models.Game.Items.Actions.ItemTaskType.Auction, newItem, true);
            AddAuctionItem(newAuctionItem);
            player.SendPacket(new SCAuctionPostedPacket(newAuctionItem));
        }

        public void RemoveAuctionItemSold(AuctionItem itemToRemove, string buyer, uint soldAmount)
        {
            if (_auctionItems.Contains(itemToRemove))
            {
                var itemTemplate = ItemManager.Instance.GetItemTemplateFromItemId(itemToRemove.ItemID);
                var newItem = ItemManager.Instance.Create(itemTemplate.Id, (int)itemToRemove.StackSize, itemToRemove.Grade);
                var itemList = new Item[10].ToList();
                itemList[0] = newItem;

                var moneyAfterFee = soldAmount * .9;
                var moneyToSend = new int[3];
                moneyToSend[0] = (int)moneyAfterFee;

                var emptyItemList = new Item[10].ToList();
                var emptyMoneyArray = new int[3];

                MailManager.Instance.SendMail(0, itemToRemove.ClientName, "Auction House", "Succesfull Listing", $"{GetLocalizedItemNameById(itemToRemove.ItemID)} sold!", 1, moneyToSend, 0, emptyItemList); //Send money to seller
                MailManager.Instance.SendMail(0, buyer, "Auction House", "Succesfull Purchase", "See attached.", 1, emptyMoneyArray, 1, itemList); //Send items to buyer
                RemoveAuctionItem(itemToRemove);
            }
        }

        public void RemoveAuctionItemFail(AuctionItem itemToRemove)
        {
            if (_auctionItems.Contains(itemToRemove))
            {
                if (itemToRemove.BidderName != "") //Player won the bid. 
                {
                    RemoveAuctionItemSold(itemToRemove, itemToRemove.BidderName, itemToRemove.BidMoney);
                    return;
                }
                else //Item did not sell by end of the timer. 
                {
                    var itemTemplate = ItemManager.Instance.GetItemTemplateFromItemId(itemToRemove.ItemID);
                    var newItem = ItemManager.Instance.Create(itemTemplate.Id, (int)itemToRemove.StackSize, itemToRemove.Grade);
                    var itemList = new Item[10].ToList();
                    itemList[0] = newItem;
                    MailManager.Instance.SendMail(0, itemToRemove.ClientName, "Auction House", "Failed Listing", "See attached.", 1, new int[3], 0, itemList);
                    RemoveAuctionItem(itemToRemove);
                }
            }
        }

        public void CancelAuctionItem(Character player, ulong auctionId)
        {
            var auctionItem = GetAuctionItemFromID(auctionId);

            if (auctionItem != null)
            {
                var moneyToSubtract = auctionItem.DirectMoney * .1f;
                var itemList = new Item[10].ToList();
                var newItem = ItemManager.Instance.Create(auctionItem.ItemID, (int)auctionItem.StackSize, auctionItem.Grade);
                itemList[0] = newItem;

                player.ChangeMoney(SlotType.Inventory, -(int)moneyToSubtract);
                MailManager.Instance.SendMail(0, auctionItem.ClientName, "AuctionHouse", "Cancelled Listing", "See attaached.", 1, new int[3], 0, itemList);

                RemoveAuctionItem(auctionItem);
                player.SendPacket(new SCAuctionCanceledPacket(auctionItem));
            }
        }

        public AuctionItem GetAuctionItemFromID(ulong auctionId)
        {
            for (int i = 0; i < _auctionItems.Count; i++)
            {
                if (_auctionItems[i].ID == auctionId)
                {
                    return _auctionItems[i];
                }
            }
            return null;
        }
        public void BidOnAuctionItem(Character player, ulong auctionId, uint bidAmount)
        {
            var auctionItem = GetAuctionItemFromID(auctionId);
            if (auctionItem != null)
            {
                if (bidAmount >= auctionItem.DirectMoney) //Buy now
                {
                    player.SubtractMoney(SlotType.Inventory, (int)auctionItem.DirectMoney);
                    RemoveAuctionItemSold(auctionItem, player.Name, auctionItem.DirectMoney);
                }

                else if (bidAmount > auctionItem.BidMoney) //Bid
                {
                    if (auctionItem.BidderName != "") //Send mail to old bidder. 
                    {
                        var moneyArray = new int[3];
                        moneyArray[0] = (int)auctionItem.BidMoney;
                        MailManager.Instance.SendMail(0, auctionItem.BidderName, "Auction House", "OutBid Notice", "", 1, moneyArray, 0, new Item[10].ToList());
                    }

                    //Set info to new bidders info
                    auctionItem.BidderName = player.Name;
                    auctionItem.BidMoney = bidAmount;

                    player.SubtractMoney(SlotType.Inventory, (int)bidAmount);
                    player.SendPacket(new SCAuctionBidPacket(auctionItem));
                }
            }
        }

        public List<AuctionItem> GetAuctionItems(AuctionSearchTemplate searchTemplate)
        {
            List<AuctionItem> auctionItemsFound = new List<AuctionItem>();
            bool myListing = false;

            if (searchTemplate.ItemName == "" && searchTemplate.CategoryA == 0 && searchTemplate.CategoryB == 0 && searchTemplate.CategoryC == 0)
            {
                myListing = true;
                var query = from item in _auctionItems
                            where item.ClientName == searchTemplate.Player.Name
                            select item;

                auctionItemsFound = query.ToList<AuctionItem>();
            }

            if (!myListing)
            {
                var query = from item in _auctionItems
                            where ((searchTemplate.ItemName != "") ? item.ItemName.ToLower().Contains(searchTemplate.ItemName.ToLower()) : true)
                            where ((searchTemplate.CategoryA != 0) ? searchTemplate.CategoryA == item.CategoryA : true)
                            where ((searchTemplate.CategoryB != 0) ? searchTemplate.CategoryB == item.CategoryB : true)
                            where ((searchTemplate.CategoryC != 0) ? searchTemplate.CategoryC == item.CategoryC : true)
                            select item;

                auctionItemsFound = query.ToList<AuctionItem>();
            }

            if (searchTemplate.SortKind == 1) //Price
            {
                var sortedList = auctionItemsFound.OrderByDescending(x => x.DirectMoney).ToList();
                auctionItemsFound = sortedList;
                if (searchTemplate.SortOrder == 1)
                {
                    auctionItemsFound.Reverse();
                }
            }

            //TODO 2 Name of item
            //TODO 3 Level of item

            if (searchTemplate.SortKind == 4) //TimeLeft
            {
                var sortedList = auctionItemsFound.OrderByDescending(x => x.TimeLeft).ToList();
                auctionItemsFound = sortedList;
                if (searchTemplate.SortOrder == 1)
                {
                    auctionItemsFound.Reverse();
                }
            }

            if (searchTemplate.Page > 0)
            {
                var startingItemNumber = (int)(searchTemplate.Page * 9);
                var endingitemNumber = (int)((searchTemplate.Page * 9) + 8);
                if (auctionItemsFound.Count > startingItemNumber)
                {
                    var tempItemList = new List<AuctionItem>();
                    for (var i = startingItemNumber; i < endingitemNumber; i++)
                    {
                        if (auctionItemsFound.ElementAtOrDefault(i) != null)
                        {
                            tempItemList.Add(auctionItemsFound[i]);
                        }
                    }
                    auctionItemsFound = tempItemList;
                }
                else
                {
                    searchTemplate.Page = 0;
                }
            }

            if (auctionItemsFound.Count > 9)
            {
                var tempList = new List<AuctionItem>();

                for (int i = 0; i < 9; i++)
                {
                    tempList.Add(auctionItemsFound[i]);
                }

                auctionItemsFound = tempList;
            }
            return auctionItemsFound;
        }

        public AuctionItem GetCheapestAuctionItem(ulong itemId)
        {
            var tempList = new List<AuctionItem>();

            foreach (var item in _auctionItems)
            {
                if (item.ItemID == itemId)
                {
                    tempList.Add(item);
                }
            }

            if (tempList.Count > 0)
            {
                tempList.OrderByDescending(x => x.DirectMoney);
                return tempList[0];
            }
            else
            {
                return null;
            }
        }

        public string GetLocalizedItemNameById(uint id)
        {
            return LocalizationManager.Instance.Get("items", "name", id, ItemManager.Instance.GetTemplate(id).Name ?? "");
        }

        public ulong GetNextId()
        {
            ulong nextId = 0;
            foreach (var item in _auctionItems)
            {
                if (nextId < item.ID)
                {
                    nextId = item.ID;
                }
            }
            return nextId + 1;
        }

        public void RemoveAuctionItem(AuctionItem itemToRemove)
        {
            lock (_auctionItems)
            {
                lock (_deletedAuctionItemIds)
                {
                    if (_auctionItems.Contains(itemToRemove))
                    {
                        _deletedAuctionItemIds.Add((long)itemToRemove.ID);
                        _auctionItems.Remove(itemToRemove);
                    }
                }
            }
        }

        public void AddAuctionItem(AuctionItem itemToAdd)
        {
            lock (_auctionItems)
            {
                _auctionItems.Add(itemToAdd);
            }
        }

        public void UpdateAuctionHouse()
        {
            _log.Debug("Updating Auction House!");

            foreach (var item in _auctionItems)
            {
                if (DateTime.UtcNow > item.EndTime)
                {
                    RemoveAuctionItem(item);
                }

                else
                {
                    item.TimeLeft -= 5;
                }
            }
        }

        public AuctionItem CreateAuctionItem(Character player, Item itemToList, uint startPrice, uint buyoutPrice, byte duration)
        {
            var newItem = itemToList;

            if (newItem == null) //TODO
            {
                return null;
            }

            ulong timeLeft;
            switch (duration)
            {
                case 0:
                    timeLeft = 21600; //6 hourse
                    break;
                case 1:
                    timeLeft = 43200; //12 hours
                    break;
                case 2:
                    timeLeft = 86400; //24 hours
                    break;
                case 3:
                    timeLeft = 172800; //48 hours
                    break;
                default:
                    timeLeft = 21600; //default to 6 hours
                    break;
            }

            var newAuctionItem = new AuctionItem
            {
                ID = GetNextId(),
                Duration = 5,
                ItemID = newItem.Template.Id,
                ItemName = GetLocalizedItemNameById(newItem.Template.Id),
                ObjectID = 0,
                Grade = newItem.Grade,
                Flags = newItem.ItemFlags,
                StackSize = (uint)newItem.Count,
                DetailType = 0,
                CreationTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddSeconds(timeLeft),
                LifespanMins = 0,
                Type1 = 0,
                WorldId = 0,
                UnpackDateTIme = DateTime.UtcNow,
                UnsecureDateTime = DateTime.UtcNow,
                WorldId2 = 0,
                ClientId = player.Id,
                ClientName = player.Name,
                StartMoney = startPrice,
                DirectMoney = buyoutPrice,
                TimeLeft = timeLeft,
                BidWorldID = 0,
                BidderId = 0,
                BidderName = "",
                BidMoney = 0,
                Extra = 0,
                CategoryA = (uint)newItem.Template.AuctionCategoryA,
                CategoryB = (uint)newItem.Template.AuctionCategoryB,
                CategoryC = (uint)newItem.Template.AuctionCategoryC
            };
            return newAuctionItem;
        }

        public void Load()
        {
            _auctionItems = new List<AuctionItem>();
            _deletedAuctionItemIds = new List<long>();
            using (var connection = MySQL.CreateConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM auction_house";
                    command.Prepare();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var auctionItem = new AuctionItem
                            {
                                ID = reader.GetUInt32("id"),
                                Duration = reader.GetByte("duration"), //0 is 6 hours, 1 is 12 hours, 2 is 24 hours, 3 is 48 hours
                                ItemID = reader.GetUInt32("item_id"),
                                ItemName = reader.GetString("item_name").ToLower(),
                                ObjectID = reader.GetUInt32("object_id"),
                                Grade = reader.GetByte("grade"),
                                Flags = (ItemFlag)reader.GetByte("flags"),
                                StackSize = reader.GetUInt32("stack_size"),
                                DetailType = reader.GetByte("detail_type"),
                                CreationTime = reader.GetDateTime("creation_time"),
                                EndTime = reader.GetDateTime("end_time"),
                                LifespanMins = reader.GetUInt32("lifespan_mins"),
                                Type1 = reader.GetUInt32("type_1"),
                                WorldId = reader.GetByte("world_id"),
                                UnsecureDateTime = reader.GetDateTime("unsecure_date_time"),
                                UnpackDateTIme = reader.GetDateTime("unpack_date_time"),
                                WorldId2 = reader.GetByte("world_id_2"),
                                ClientId = reader.GetUInt32("client_id"),
                                ClientName = reader.GetString("client_name"),
                                StartMoney = reader.GetUInt32("start_money"),
                                DirectMoney = reader.GetUInt32("direct_money"),
                                TimeLeft = reader.GetUInt32("time_left"),
                                BidWorldID = reader.GetByte("bid_world_id"),
                                BidderId = reader.GetUInt32("bidder_id"),
                                BidderName = reader.GetString("bidder_name"),
                                BidMoney = reader.GetUInt32("bid_money"),
                                Extra = reader.GetUInt32("extra"),
                                CategoryA = reader.GetUInt32("category_a"),
                                CategoryB = reader.GetUInt32("category_b"),
                                CategoryC = reader.GetUInt32("category_c")
                            };
                            AddAuctionItem(auctionItem);
                        }
                    }
                }
            }
            var auctionTask = new AuctionHouseTask();
            TaskManager.Instance.Schedule(auctionTask, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }
        public (int, int) Save(MySqlConnection connection, MySqlTransaction transaction)
        {
            var deletedCount = 0;
            var updatedCount = 0;

            lock (_deletedAuctionItemIds)
            {
                deletedCount = _deletedAuctionItemIds.Count;
                if (_deletedAuctionItemIds.Count > 0)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Connection = connection;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM auction_house WHERE `id` IN(" + string.Join(",", _deletedAuctionItemIds) + ")";
                        command.Prepare();
                        command.ExecuteNonQuery();
                    }
                    _deletedAuctionItemIds.Clear();
                }
            }

            foreach (var mtbs in _auctionItems)
            {
                using (var command = connection.CreateCommand())
                {
                    command.Connection = connection;
                    command.Transaction = transaction;
                    command.CommandText = "REPLACE INTO auction_house(" +
                        "`id`, `duration`, `item_id`, `object_id`, `grade`, `flags`, `stack_size`, `detail_type`," +
                        " `creation_time`,`end_time`, `lifespan_mins`, `type_1`, `world_id`, `unsecure_date_time`, `unpack_date_time`," +
                        " `world_id_2`, `client_id`, `client_name`, `start_money`, `direct_money`, `time_left`, `bid_world_id`," +
                        " `bidder_id`, `bidder_name`, `bid_money`, `extra`, `item_name`, `category_a`, `category_b`, `category_c`" +
                        ") VALUES (" +
                        "@id, @duration, @item_id, @object_id, @grade, @flags, @stack_size, @detail_type," +
                        " @creation_time, @end_time, @lifespan_mins, @type_1, @world_id, @unsecure_date_time, @unpack_date_time," +
                        " @world_id_2, @client_id, @client_name, @start_money, @direct_money, @time_left, @bid_world_id," +
                        " @bidder_id, @bidder_name, @bid_money, @extra, @item_name, @category_a, @category_b, @category_c)";

                    command.Prepare();

                    command.Parameters.AddWithValue("@id", mtbs.ID);
                    command.Parameters.AddWithValue("@duration", mtbs.Duration);
                    command.Parameters.AddWithValue("@item_id", mtbs.ItemID);
                    command.Parameters.AddWithValue("@object_id", mtbs.ObjectID);
                    command.Parameters.AddWithValue("@grade", mtbs.Grade);
                    command.Parameters.AddWithValue("@flags", mtbs.Flags);
                    command.Parameters.AddWithValue("@stack_size", mtbs.StackSize);
                    command.Parameters.AddWithValue("@detail_type", mtbs.DetailType);
                    command.Parameters.AddWithValue("@creation_time", mtbs.CreationTime);
                    command.Parameters.AddWithValue("@end_time", mtbs.EndTime);
                    command.Parameters.AddWithValue("@lifespan_mins", mtbs.LifespanMins);
                    command.Parameters.AddWithValue("@type_1", mtbs.Type1);
                    command.Parameters.AddWithValue("@world_id", mtbs.WorldId);
                    command.Parameters.AddWithValue("@unsecure_date_time", mtbs.UnsecureDateTime);
                    command.Parameters.AddWithValue("@unpack_date_time", mtbs.UnpackDateTIme);
                    command.Parameters.AddWithValue("@world_id_2", mtbs.WorldId2);
                    command.Parameters.AddWithValue("@client_id", mtbs.ClientId);
                    command.Parameters.AddWithValue("@client_name", mtbs.ClientName);
                    command.Parameters.AddWithValue("@start_money", mtbs.StartMoney);
                    command.Parameters.AddWithValue("@direct_money", mtbs.DirectMoney);
                    command.Parameters.AddWithValue("@time_left", mtbs.TimeLeft);
                    command.Parameters.AddWithValue("@bid_world_id", mtbs.BidWorldID);
                    command.Parameters.AddWithValue("@bidder_id", mtbs.BidderId);
                    command.Parameters.AddWithValue("@bidder_name", mtbs.BidderName);
                    command.Parameters.AddWithValue("@bid_money", mtbs.BidMoney);
                    command.Parameters.AddWithValue("@extra", mtbs.Extra);
                    command.Parameters.AddWithValue("@item_name", mtbs.ItemName);
                    command.Parameters.AddWithValue("@category_a", mtbs.CategoryA);
                    command.Parameters.AddWithValue("@category_b", mtbs.CategoryB);
                    command.Parameters.AddWithValue("@category_c", mtbs.CategoryC);

                    command.ExecuteNonQuery();
                    updatedCount++;
                }

            }

            return (updatedCount, deletedCount);
        }
    }
}
