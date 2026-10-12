export type Message = {
  id: string;
  conversationId: string;
  senderId: string;
  clientMessageId: string;
  content: string;
  sentAt: string;
};
export type Conversation = {
  id: string;
  matchId: string;
  partnerId: string;
  partnerName: string;
  createdAt: string;
  lastMessage: Message | null;
  canSend: boolean;
};
export type MessagePage = {
  items: Message[];
  nextCursor: string | null;
  nextAfterId: string | null;
};
export type ConversationPage = {
  items: Conversation[];
  page: number;
  pageSize: number;
  hasMore: boolean;
};
export type ChatNotification = {
  id: string;
  type: 'match' | 'newMessage';
  targetId: string;
  createdAt: string;
  readAt: string | null;
};
export type NotificationPage = {
  items: ChatNotification[];
  page: number;
  pageSize: number;
  hasMore: boolean;
};
export type Delivery = { clientMessageId: string; content: string; status: 'sending' | 'failed' };
