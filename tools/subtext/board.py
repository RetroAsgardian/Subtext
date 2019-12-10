#!/usr/bin/env python3
"""
subtext.board - Subtext board API.
"""
from typing import Optional
import requests, base64, hashlib
from uuid import UUID
from datetime import datetime
from enum import Enum

from .common import _assert_compatibility, VersionError, APIError, PagedList

class BoardEncryption(Enum):
	none = 'None'
	shared_key = 'SharedKey'
	gnupg = 'GnuPG'

class BoardAPI:
	"""
	Subtext board API class.
	"""
	def __init__(self, url: str, version: str, **config):
		self.url = url
		self.version = version
		self.config = config
	
	def create_board(self, session_id: UUID, name: str, encryption: BoardEncryption = BoardEncryption.gnupg):
		resp = requests.post(self.url + "/Subtext/board/create", params={
			'sessionId': session_id,
			'name': name,
			'encryption': encryption.value
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def create_board_direct(self, session_id: UUID, recipient_id: UUID):
		resp = requests.post(self.url + "/Subtext/board/createdirect", params={
			'sessionId': session_id,
			'recipientId': recipient_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_boards(self, session_id: UUID, start: Optional[int] = None, count: Optional[int] = None, only_owned: Optional[bool] = None):
		resp = requests.get(self.url + "/Subtext/board", params={
			'sessionId': session_id,
			'start': start,
			'count': count,
			'onlyOwned': only_owned
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_board(self, session_id: UUID, board_id: UUID):
		resp = requests.get(self.url + "/Subtext/board/{}".format(board_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_board_members(self, session_id: UUID, board_id: UUID, start: Optional[int] = None, count: Optional[int] = None):
		resp = requests.get(self.url + "/Subtext/board/{}/members".format(board_id), params={
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
	
	def get_board_messages(self, session_id: UUID, board_id: UUID, start: Optional[int] = None, count: Optional[int] = None):
		resp = requests.get(self.url + "/Subtext/board/{}/messages".format(board_id), params={
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
	
