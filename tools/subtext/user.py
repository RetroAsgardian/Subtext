#!/usr/bin/env python3
"""
subtext.user - Subtext user API.
"""
from typing import Optional
import requests
from uuid import UUID
from enum import Enum
from datetime import datetime

class UserPresence(Enum):
	offline = 'Offline'
	online = 'Online'
	away = 'Away'
	busy = 'Busy'

from .common import _assert_compatibility, VersionError, APIError, PagedList

class UserAPI:
	"""
	Subtext user API class.
	"""
	def __init__(self, url: str, version: str, **config):
		self.url = url
		self.version = version
		self.config = config
	
	def create(self, name: str, password: str, public_key: bytes = bytes(0)):
		resp = requests.post(self.url + "/Subtext/user/create", data=public_key, params={
			'name': name,
			'password': password
		}, headers={'Content-Type': 'application/octet-stream'})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def query_id_by_name(self, name: str):
		resp = requests.get(self.url + "/Subtext/user/queryidbyname", params={
			'name': name
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def login(self, user_id: UUID, password: str):
		resp = requests.post(self.url + "/Subtext/user/login", params={
			'userId': user_id,
			'password': password
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
	def heartbeat(self, session_id: UUID):
		resp = requests.post(self.url + "/Subtext/user/heartbeat", params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
	def logout(self, session_id: UUID):
		resp = requests.post(self.url + "/Subtext/user/logout", params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
	def get(self, session_id: UUID, user_id: UUID):
		resp = requests.get(self.url + "/Subtext/user/{}".format(user_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_friends(self, session_id: UUID, user_id: UUID, start: Optional[int] = None, count: Optional[int] = None):
		resp = requests.get(self.url + "/Subtext/user/{}/friends".format(user_id), params={
			'sessionId': session_id,
			'start': start,
			'count': count
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def remove_friend(self, session_id: UUID, user_id: UUID, friend_id: UUID):
		resp = requests.delete(self.url + "/Subtext/user/{}/friends/{}".format(user_id, friend_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_blocked_users(self, session_id: UUID, user_id: UUID, start: Optional[int] = None, count: Optional[int] = None):
		resp = requests.get(self.url + "/Subtext/user/{}/blocked".format(user_id), params={
			'sessionId': session_id,
			'start': start,
			'count': count
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def add_blocked_user(self, session_id: UUID, user_id: UUID, blocked_id: UUID):
		resp = requests.post(self.url + "/Subtext/user/{}/blocked".format(user_id), params={
			'sessionId': session_id,
			'blockedId': blocked_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def remove_blocked_user(self, session_id: UUID, user_id: UUID, blocked_id: UUID):
		resp = requests.delete(self.url + "/Subtext/user/{}/blocked/{}".format(user_id, blocked_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_friend_requests(self, session_id: UUID, user_id: UUID, start: Optional[int] = None, count: Optional[int] = None):
		resp = requests.get(self.url + "/Subtext/user/{}/friendrequests".format(user_id), params={
			'sessionId': session_id,
			'start': start,
			'count': count
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def send_friend_request(self, session_id: UUID, user_id: UUID):
		resp = requests.post(self.url + "/Subtext/user/{}/friendrequests".format(user_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def accept_friend_request(self, session_id: UUID, user_id: UUID, sender_id: UUID):
		resp = requests.post(self.url + "/Subtext/user/{}/friendrequests/{}".format(user_id, sender_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def reject_friend_request(self, session_id: UUID, user_id: UUID, sender_id: UUID):
		resp = requests.delete(self.url + "/Subtext/user/{}/friendrequests/{}".format(user_id, sender_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_keys(self, session_id: UUID, user_id: UUID, start: Optional[int] = None, count: Optional[int] = None):
		resp = requests.get(self.url + "/Subtext/user/{}/keys".format(user_id), params={
			'sessionId': session_id,
			'start': start,
			'count': count
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def add_key(self, session_id: UUID, user_id: UUID, public_key: bytes):
		resp = requests.post(self.url + "/Subtext/user/{}/keys".format(user_id), data=public_key, params={
			'sessionId': session_id
		}, headers={'Content-Type': 'application/octet-stream'})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def set_presence(self, session_id: UUID, user_id: UUID, presence: UserPresence, until_time: Optional[datetime] = None, other_data: str = ""):
		resp = requests.put(self.url + "/Subtext/user/{}/presence".format(user_id), params={
			'sessionId': session_id,
			'presence': presence,
			'untilTime': until_time,
			'otherData': other_data
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def delete(self, session_id: UUID, user_id: UUID, password: str):
		resp = requests.delete(self.url + "/Subtext/user/{}".format(user_id), params={
			'sessionId': session_id,
			'password': password
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
