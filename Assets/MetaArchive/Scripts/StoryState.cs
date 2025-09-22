public enum StoryState
{
    // 초기화
    Game_Start_Screen,
    Player_Name_Input,
    Intro_Meet_Dungddangi,

    // 콘텐츠 제작실
    Move_To_Content_Room,
    Meet_Minjae_In_Content_Room,
    Quest_Find_Keyboard,
    Found_Keyboard,
    Quest_Deliver_Keyboard,
    Delivered_Keyboard,
    Content_Room_Explained,

    // 쇼룸
    Move_To_ShowRoom,
    Meet_Sukyung_In_Showroom,
    Quest_Find_VRGogi,
    Found_VRGogi,
    Quest_Deliver_VRGogi,
    Delivered_VRGogi,
    Showroom_Explained,

    // AR/VR 실습실
    Move_To_ARVR_Room,
    Meet_Sehee_In_ARVR_Room,
    Quest_Find_Tablet,
    Found_Tablet,
    Quest_Deliver_Tablet,
    Delivered_Tablet,
    ARVR_Room_Explained,

    // 마무리
    Ending_With_Dungddangi,
    Give_Gift_Prompt,
    Gift_Received,
    Final_Ending_Prompt,
    Game_Ended
}
