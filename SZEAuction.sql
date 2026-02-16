--
-- PostgreSQL database dump
--

\restrict OzGEkvYoR4Fbaz6hMqUjk420KzUNJXo9huZqSiATuevPqwMqchZsoyRGYBgsrHq

-- Dumped from database version 16.11
-- Dumped by pg_dump version 16.11

-- Started on 2026-02-16 11:22:58

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 219 (class 1259 OID 24596)
-- Name: auction_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.auction_items (
    auction_item_id integer NOT NULL,
    seller_user_id integer NOT NULL,
    title character varying(255) NOT NULL,
    description text,
    auction_state_id integer NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    close_time timestamp with time zone NOT NULL,
    start_price numeric(12,2) DEFAULT 0 NOT NULL,
    min_increment numeric(12,2) DEFAULT 0 NOT NULL,
    winning_bid_id integer,
    closed_at timestamp with time zone,
    CONSTRAINT ck_auction_items_close_time_after_create CHECK ((close_time > created_at)),
    CONSTRAINT ck_auction_items_prices_nonneg CHECK (((start_price >= (0)::numeric) AND (min_increment >= (0)::numeric)))
);


ALTER TABLE public.auction_items OWNER TO postgres;

--
-- TOC entry 218 (class 1259 OID 24595)
-- Name: auction_items_auction_item_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.auction_items ALTER COLUMN auction_item_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.auction_items_auction_item_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 217 (class 1259 OID 24588)
-- Name: auction_states; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.auction_states (
    auction_state_id integer NOT NULL,
    name character varying(50) NOT NULL
);


ALTER TABLE public.auction_states OWNER TO postgres;

--
-- TOC entry 221 (class 1259 OID 24619)
-- Name: bids; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.bids (
    bid_id integer NOT NULL,
    auction_item_id integer NOT NULL,
    bidder_user_id integer NOT NULL,
    amount numeric(12,2) NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_bids_amount_positive CHECK ((amount > (0)::numeric))
);


ALTER TABLE public.bids OWNER TO postgres;

--
-- TOC entry 220 (class 1259 OID 24618)
-- Name: bids_bid_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.bids ALTER COLUMN bid_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.bids_bid_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 223 (class 1259 OID 24642)
-- Name: notifications; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.notifications (
    notification_id integer NOT NULL,
    user_id integer NOT NULL,
    auction_item_id integer,
    type character varying(50) NOT NULL,
    subject character varying(255),
    body text NOT NULL,
    status smallint DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    sent_at timestamp with time zone,
    last_error text,
    attempt_count integer DEFAULT 0 NOT NULL,
    CONSTRAINT ck_notifications_attempt_nonneg CHECK ((attempt_count >= 0)),
    CONSTRAINT ck_notifications_status_range CHECK ((status = ANY (ARRAY[0, 1, 2])))
);


ALTER TABLE public.notifications OWNER TO postgres;

--
-- TOC entry 222 (class 1259 OID 24641)
-- Name: notifications_notification_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.notifications ALTER COLUMN notification_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.notifications_notification_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 216 (class 1259 OID 24578)
-- Name: users; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.users (
    user_id integer NOT NULL,
    username character varying(255) NOT NULL,
    password_hash text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.users OWNER TO postgres;

--
-- TOC entry 215 (class 1259 OID 24577)
-- Name: users_user_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.users ALTER COLUMN user_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.users_user_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- TOC entry 4776 (class 2606 OID 24607)
-- Name: auction_items auction_items_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.auction_items
    ADD CONSTRAINT auction_items_pkey PRIMARY KEY (auction_item_id);


--
-- TOC entry 4772 (class 2606 OID 24594)
-- Name: auction_states auction_states_name_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.auction_states
    ADD CONSTRAINT auction_states_name_key UNIQUE (name);


--
-- TOC entry 4774 (class 2606 OID 24592)
-- Name: auction_states auction_states_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.auction_states
    ADD CONSTRAINT auction_states_pkey PRIMARY KEY (auction_state_id);


--
-- TOC entry 4780 (class 2606 OID 24625)
-- Name: bids bids_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.bids
    ADD CONSTRAINT bids_pkey PRIMARY KEY (bid_id);


--
-- TOC entry 4784 (class 2606 OID 24653)
-- Name: notifications notifications_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT notifications_pkey PRIMARY KEY (notification_id);


--
-- TOC entry 4768 (class 2606 OID 24585)
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (user_id);


--
-- TOC entry 4770 (class 2606 OID 24587)
-- Name: users users_username_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_username_key UNIQUE (username);


--
-- TOC entry 4777 (class 1259 OID 24665)
-- Name: ix_auction_items_seller_created; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ix_auction_items_seller_created ON public.auction_items USING btree (seller_user_id, created_at);


--
-- TOC entry 4778 (class 1259 OID 24664)
-- Name: ix_auction_items_state_close; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ix_auction_items_state_close ON public.auction_items USING btree (auction_state_id, close_time);


--
-- TOC entry 4781 (class 1259 OID 24666)
-- Name: ix_bids_item_amount_created; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ix_bids_item_amount_created ON public.bids USING btree (auction_item_id, amount, created_at, bid_id);


--
-- TOC entry 4782 (class 1259 OID 24667)
-- Name: ix_notifications_status_created; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX ix_notifications_status_created ON public.notifications USING btree (status, created_at);


--
-- TOC entry 4785 (class 2606 OID 24608)
-- Name: auction_items fk_auction_items_seller; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.auction_items
    ADD CONSTRAINT fk_auction_items_seller FOREIGN KEY (seller_user_id) REFERENCES public.users(user_id) ON DELETE RESTRICT;


--
-- TOC entry 4786 (class 2606 OID 24613)
-- Name: auction_items fk_auction_items_state; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.auction_items
    ADD CONSTRAINT fk_auction_items_state FOREIGN KEY (auction_state_id) REFERENCES public.auction_states(auction_state_id) ON DELETE RESTRICT;


--
-- TOC entry 4787 (class 2606 OID 24636)
-- Name: auction_items fk_auction_items_winning_bid; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.auction_items
    ADD CONSTRAINT fk_auction_items_winning_bid FOREIGN KEY (winning_bid_id) REFERENCES public.bids(bid_id) ON DELETE SET NULL;


--
-- TOC entry 4788 (class 2606 OID 24631)
-- Name: bids fk_bids_bidder; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.bids
    ADD CONSTRAINT fk_bids_bidder FOREIGN KEY (bidder_user_id) REFERENCES public.users(user_id) ON DELETE RESTRICT;


--
-- TOC entry 4789 (class 2606 OID 24626)
-- Name: bids fk_bids_item; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.bids
    ADD CONSTRAINT fk_bids_item FOREIGN KEY (auction_item_id) REFERENCES public.auction_items(auction_item_id) ON DELETE CASCADE;


--
-- TOC entry 4790 (class 2606 OID 24659)
-- Name: notifications fk_notifications_item; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT fk_notifications_item FOREIGN KEY (auction_item_id) REFERENCES public.auction_items(auction_item_id) ON DELETE SET NULL;


--
-- TOC entry 4791 (class 2606 OID 24654)
-- Name: notifications fk_notifications_user; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT fk_notifications_user FOREIGN KEY (user_id) REFERENCES public.users(user_id) ON DELETE CASCADE;


-- Completed on 2026-02-16 11:22:58

--
-- PostgreSQL database dump complete
--

\unrestrict OzGEkvYoR4Fbaz6hMqUjk420KzUNJXo9huZqSiATuevPqwMqchZsoyRGYBgsrHq

