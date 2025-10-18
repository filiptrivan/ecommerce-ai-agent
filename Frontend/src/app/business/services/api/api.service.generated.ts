import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiSecurityService, Filter, PaginatedResult, Namebook, Codebook, LazyLoadSelectedIdsResult, VerificationTokenRequest, AuthResult, ExternalProvider } from 'spiderly';
import { ConfigService } from '../config.service';
import { Message } from '../../entities/business-entities.generated';
import { Notification } from '../../entities/business-entities.generated';
import { NotificationSaveBody } from '../../entities/business-entities.generated';
import { QdrantProduct } from '../../entities/business-entities.generated';
import { Agent } from '../../entities/business-entities.generated';
import { AgentSaveBody } from '../../entities/business-entities.generated';
import { AgentMainUIForm } from '../../entities/business-entities.generated';
import { NotificationMainUIForm } from '../../entities/business-entities.generated';
import { User } from '../../entities/business-entities.generated';
import { UserSaveBody } from '../../entities/business-entities.generated';
import { UserMainUIForm } from '../../entities/business-entities.generated';
import { UserNotification } from '../../entities/business-entities.generated';
import { UserNotificationSaveBody } from '../../entities/business-entities.generated';
import { UserNotificationMainUIForm } from '../../entities/business-entities.generated';

@Injectable({
    providedIn: 'root'
})
export class ApiGeneratedService extends ApiSecurityService {

    constructor(
        protected override http: HttpClient,
        protected override config: ConfigService
    ) {
        super(http, config);
    }

    saveProductsToVectorDb = (): Observable<any> => { 
        return this.http.get(`${this.config.apiUrl}/Agent/SaveProductsToVectorDb`, this.config.httpOptions);
    }

    sendMessage = (messageDTO: Message): Observable<string> => { 
        return this.http.post(`${this.config.apiUrl}/Agent/SendMessage`, messageDTO, { ...this.config.httpOptions, responseType: 'text' });
    }

    sendNotificationEmail = (notificationId: number, notificationVersion: number): Observable<any> => { 
        return this.http.get(`${this.config.apiUrl}/Notification/SendNotificationEmail?notificationId=${notificationId}&notificationVersion=${notificationVersion}`, this.config.httpOptions);
    }

    deleteNotificationForCurrentUser = (notificationId: number, notificationVersion: number): Observable<any> => { 
        return this.http.delete(`${this.config.apiUrl}/Notification/DeleteNotificationForCurrentUser?notificationId=${notificationId}&notificationVersion=${notificationVersion}`, this.config.httpOptions);
    }

    markNotificationAsReadForCurrentUser = (notificationId: number, notificationVersion: number): Observable<any> => { 
        return this.http.get(`${this.config.apiUrl}/Notification/MarkNotificationAsReadForCurrentUser?notificationId=${notificationId}&notificationVersion=${notificationVersion}`, this.config.httpOptions);
    }

    markNotificationAsUnreadForCurrentUser = (notificationId: number, notificationVersion: number): Observable<any> => { 
        return this.http.get(`${this.config.apiUrl}/Notification/MarkNotificationAsUnreadForCurrentUser?notificationId=${notificationId}&notificationVersion=${notificationVersion}`, this.config.httpOptions);
    }

    getNotificationsForCurrentUser = (filterDTO: Filter): Observable<PaginatedResult<Notification>> => { 
        return this.http.post<PaginatedResult<Notification>>(`${this.config.apiUrl}/Notification/GetNotificationsForCurrentUser`, filterDTO, this.config.httpSkipSpinnerOptions);
    }

    getCurrentUser = (): Observable<User> => { 
        return this.http.get<User>(`${this.config.apiUrl}/User/GetCurrentUser`, this.config.httpSkipSpinnerOptions);
    }



    getPaginatedAgentList = (filterDTO: Filter): Observable<PaginatedResult<Agent>> => { 
        return this.http.post<PaginatedResult<Agent>>(`${this.config.apiUrl}/Agent/GetPaginatedAgentList`, filterDTO, this.config.httpSkipSpinnerOptions);
    }

    exportAgentListToExcel = (filterDTO: Filter): Observable<any> => { 
        return this.http.post(`${this.config.apiUrl}/Agent/ExportAgentListToExcel`, filterDTO, { observe: 'response', responseType: 'blob' });
    }

    getAgentList = (): Observable<Agent[]> => { 
        return this.http.get<Agent[]>(`${this.config.apiUrl}/Agent/GetAgentList`, this.config.httpOptions);
    }

    getAgentMainUIFormDTO = (id: number): Observable<AgentMainUIForm> => { 
        return this.http.get<AgentMainUIForm>(`${this.config.apiUrl}/Agent/GetAgentMainUIFormDTO?id=${id}`, this.config.httpOptions);
    }

    getAgent = (id: number): Observable<Agent> => { 
        return this.http.get<Agent>(`${this.config.apiUrl}/Agent/GetAgent?id=${id}`, this.config.httpOptions);
    }









    saveAgent = (saveBodyDTO: AgentSaveBody): Observable<AgentSaveBody> => { 
        return this.http.put<AgentSaveBody>(`${this.config.apiUrl}/Agent/SaveAgent`, saveBodyDTO, this.config.httpOptions);
    }



    deleteAgent = (id: number): Observable<any> => { 
        return this.http.delete(`${this.config.apiUrl}/Agent/DeleteAgent?id=${id}`, this.config.httpOptions);
    }


    getPaginatedNotificationList = (filterDTO: Filter): Observable<PaginatedResult<Notification>> => { 
        return this.http.post<PaginatedResult<Notification>>(`${this.config.apiUrl}/Notification/GetPaginatedNotificationList`, filterDTO, this.config.httpSkipSpinnerOptions);
    }

    exportNotificationListToExcel = (filterDTO: Filter): Observable<any> => { 
        return this.http.post(`${this.config.apiUrl}/Notification/ExportNotificationListToExcel`, filterDTO, { observe: 'response', responseType: 'blob' });
    }

    getNotificationList = (): Observable<Notification[]> => { 
        return this.http.get<Notification[]>(`${this.config.apiUrl}/Notification/GetNotificationList`, this.config.httpOptions);
    }

    getNotificationMainUIFormDTO = (id: number): Observable<NotificationMainUIForm> => { 
        return this.http.get<NotificationMainUIForm>(`${this.config.apiUrl}/Notification/GetNotificationMainUIFormDTO?id=${id}`, this.config.httpOptions);
    }

    getNotification = (id: number): Observable<Notification> => { 
        return this.http.get<Notification>(`${this.config.apiUrl}/Notification/GetNotification?id=${id}`, this.config.httpOptions);
    }







    getPaginatedRecipientsListForNotification = (filterDTO: Filter): Observable<PaginatedResult<User>> => { 
        return this.http.post<PaginatedResult<User>>(`${this.config.apiUrl}/Notification/GetPaginatedRecipientsListForNotification`, filterDTO, this.config.httpSkipSpinnerOptions);
    }

    exportRecipientsListToExcelForNotification = (filterDTO: Filter): Observable<any> => { 
        return this.http.post(`${this.config.apiUrl}/Notification/ExportRecipientsListToExcelForNotification`, filterDTO, { observe: 'response', responseType: 'blob' });
    }

    lazyLoadSelectedRecipientsIdsForNotification = (filterDTO: Filter): Observable<LazyLoadSelectedIdsResult> => { 
        return this.http.post<LazyLoadSelectedIdsResult>(`${this.config.apiUrl}/Notification/LazyLoadSelectedRecipientsIdsForNotification`, filterDTO, this.config.httpSkipSpinnerOptions);
    }

    saveNotification = (saveBodyDTO: NotificationSaveBody): Observable<NotificationSaveBody> => { 
        return this.http.put<NotificationSaveBody>(`${this.config.apiUrl}/Notification/SaveNotification`, saveBodyDTO, this.config.httpOptions);
    }



    deleteNotification = (id: number): Observable<any> => { 
        return this.http.delete(`${this.config.apiUrl}/Notification/DeleteNotification?id=${id}`, this.config.httpOptions);
    }


    getPaginatedUserList = (filterDTO: Filter): Observable<PaginatedResult<User>> => { 
        return this.http.post<PaginatedResult<User>>(`${this.config.apiUrl}/User/GetPaginatedUserList`, filterDTO, this.config.httpSkipSpinnerOptions);
    }

    exportUserListToExcel = (filterDTO: Filter): Observable<any> => { 
        return this.http.post(`${this.config.apiUrl}/User/ExportUserListToExcel`, filterDTO, { observe: 'response', responseType: 'blob' });
    }

    getUserList = (): Observable<User[]> => { 
        return this.http.get<User[]>(`${this.config.apiUrl}/User/GetUserList`, this.config.httpOptions);
    }

    getUserMainUIFormDTO = (id: number): Observable<UserMainUIForm> => { 
        return this.http.get<UserMainUIForm>(`${this.config.apiUrl}/User/GetUserMainUIFormDTO?id=${id}`, this.config.httpOptions);
    }

    getUser = (id: number): Observable<User> => { 
        return this.http.get<User>(`${this.config.apiUrl}/User/GetUser?id=${id}`, this.config.httpOptions);
    }









    saveUser = (saveBodyDTO: UserSaveBody): Observable<UserSaveBody> => { 
        return this.http.put<UserSaveBody>(`${this.config.apiUrl}/User/SaveUser`, saveBodyDTO, this.config.httpOptions);
    }



    deleteUser = (id: number): Observable<any> => { 
        return this.http.delete(`${this.config.apiUrl}/User/DeleteUser?id=${id}`, this.config.httpOptions);
    }


}
